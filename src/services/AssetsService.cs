using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Pannella.Helpers;

namespace Pannella.Services;

public class AssetsService
{
    internal static string BLACKLIST_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";
    private const string BLACKLIST_FILE = "blacklist.json";

    // Set PUPDATE_LOCAL_FILES=true to force local-file loading (e.g. dev mode via launchSettings.json),
    // overriding the use_local_blacklist setting.
    private static bool FORCE_LOCAL_FILES => string.Equals(
        Environment.GetEnvironmentVariable("PUPDATE_LOCAL_FILES"), "true",
        StringComparison.OrdinalIgnoreCase);

    private readonly bool useLocalBlacklist;
    private readonly bool showStackTraces;
    private List<string> blacklist;

    public List<string> Blacklist
    {
        get
        {
            if (this.blacklist == null)
            {
                string json = null;
                if (useLocalBlacklist || FORCE_LOCAL_FILES)
                {
                    if (File.Exists(BLACKLIST_FILE))
                    {
                        json = File.ReadAllText(BLACKLIST_FILE);
                    }
                    else
                    {
                        Console.WriteLine($"Local file not found: {BLACKLIST_FILE}");
                    }
                }
                else
                {
                    try
                    {
                        json = HttpHelper.Instance.GetHTML(BLACKLIST_END_POINT);
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"There was a error downloading the {BLACKLIST_FILE} file from GitHub.");
                        Console.WriteLine(ex.Message);
                    }
                }
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        this.blacklist = JsonConvert.DeserializeObject<List<string>>(json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"There was an error parsing the {BLACKLIST_FILE} file.");
                        
                        if (this.showStackTraces)
                        {
                            Console.WriteLine(ex);
                        }
                        else
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
                else
                {
                    this.blacklist = new List<string>();
                }
            }

            return this.blacklist;
        }
    }

    public AssetsService(bool useLocalBlacklist, bool showStackTraces)
    {
        this.useLocalBlacklist = useLocalBlacklist;
        this.showStackTraces = showStackTraces;
    }

    public bool IsBlacklisted(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return false;

        string baseName = Path.GetFileName(filename);

        return Blacklist.Any(entry =>
        {
            if (entry.Contains('*') || entry.Contains('?'))
            {
                return FileSystemName.MatchesSimpleExpression(entry, filename, ignoreCase: true)
                    || FileSystemName.MatchesSimpleExpression(entry, baseName, ignoreCase: true);
            }

            return string.Equals(entry, filename, StringComparison.Ordinal)
                || string.Equals(entry, baseName, StringComparison.Ordinal);
        });
    }

    public static void BackupSaves(string directory, string backupLocation, Action<string> log = null)
    {
        BackupDirectory(directory, "Saves", backupLocation, log ?? Console.WriteLine);
    }

    public static void BackupMemories(string directory, string backupLocation, Action<string> log = null)
    {
        BackupDirectory(directory, "Memories", backupLocation, log ?? Console.WriteLine);
    }

    // log lets callers (e.g. the TUI) redirect output away from Console; defaults to Console.WriteLine.
    private static void BackupDirectory(string rootDirectory, string folderName, string backupLocation, Action<string> log)
    {
        if (string.IsNullOrEmpty(rootDirectory))
        {
            throw new ArgumentNullException(nameof(rootDirectory));
        }

        if (string.IsNullOrEmpty(backupLocation))
        {
            throw new ArgumentNullException(nameof(backupLocation));
        }

        log($"Compressing and backing up {folderName} directory...");

        string savesPath = Path.Combine(rootDirectory, folderName);

        if (Directory.Exists(savesPath))
        {
            string directoryHash = ComputeDirectoryHash(savesPath);
            string truncatedHash = directoryHash.Substring(0, 8);
            string dateStamp = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
            string fileName = $"{folderName}_Backup_{dateStamp}_version_{truncatedHash}.zip";
            string archiveName = Path.Combine(backupLocation, fileName);

            if (!Directory.Exists(backupLocation))
            {
                Directory.CreateDirectory(backupLocation);
            }

            bool isDuplicateBackup =
                Directory.GetFiles(backupLocation, $"{folderName}_Backup_*_version_{truncatedHash}.zip")
                         .Any();

            if (!isDuplicateBackup)
            {
                ZipFile.CreateFromDirectory(savesPath, archiveName);
                log("Complete.");
            }
            else
            {
                log("Backup with the same contents already exists, skipping...");
            }
        }
        else
        {
            log($"No {folderName} directory found, skipping backup...");
        }
    }

    private static string ComputeDirectoryHash(string directoryPath)
    {
        using var sha256 = SHA256.Create();

        var allFiles = Directory
            .GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .OrderBy(p => p)
            .ToList();

        var hashBuilder = new StringBuilder();

        foreach (var filePath in allFiles)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            byte[] hashBytes = sha256.ComputeHash(fileBytes);

            hashBuilder.Append(BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant());
        }

        byte[] finalHashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashBuilder.ToString()));

        return BitConverter.ToString(finalHashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static void PruneSaveStates(string rootDirectory, string coreName = null, Action<string> log = null,
        int? keep = null)
    {
        log ??= Console.WriteLine;
        int keepCount = Math.Max(1, keep ?? ServiceHelper.SettingsService.Config.save_states_to_keep);
        BackupMemories(ServiceHelper.UpdateDirectory, ServiceHelper.SettingsService.Config.backup_saves_location, log);
        string savesPath = Path.Combine(rootDirectory, "Memories", "Save States");

        //YYYYMMDD_HHMMSS_SOMETHING_SOMETHING_GAMETITLE.STA
        const string pattern = @"^(\d\d\d\d\d\d\d\d_\d\d\d\d\d\d)_[A-Za-z]+_[A-Za-z0-9]+_(.*)\.sta$";
        Regex regex = new Regex(pattern);

        foreach (var dir in Directory.EnumerateDirectories(savesPath) )
        {
            //just skip it if it's not the requested core
            if (coreName != null && dir != Path.Combine(savesPath, coreName))
            {
                continue;
            }

            var games = Directory.GetFiles(dir, "*.sta")
                .Select(file => (file, match: regex.Match(Path.GetFileName(file))))
                .Where(x => x.match.Success)
                .GroupBy(x => x.match.Groups[2].Value);

            foreach (var game in games)
            {
                // Newest first; delete everything past the first keepCount
                var stale = game
                    .OrderByDescending(x => long.Parse(x.match.Groups[1].Value.Replace("_", string.Empty)))
                    .Skip(keepCount);

                foreach (var (file, _) in stale)
                {
                    File.Delete(file);
                    log($"Deleted file: {Path.GetFileName(file)}");
                }
            }
        }

        log(keepCount == 1
            ? "Pruning completed. Most recent save state for each game retained."
            : $"Pruning completed. {keepCount} most recent save states for each game retained.");
    }
}
