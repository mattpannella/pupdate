using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Pannella.Helpers;

namespace Pannella.Services;

public class AssetsService
{
    private const string BLACKLIST_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";
    private const string BLACKLIST_FILE = "blacklist.json";

    private readonly bool useLocalBlacklist;
    private List<string> blacklist;

    public List<string> Blacklist
    {
        get
        {
            if (this.blacklist == null)
            {
                string json = null;
#if !DEBUG
                if (useLocalBlacklist)
                {
#endif
                    if (File.Exists(BLACKLIST_FILE))
                    {
                        json = File.ReadAllText(BLACKLIST_FILE);
                    }
                    else
                    {
                        Console.WriteLine($"Local file not found: {BLACKLIST_FILE}");
                    }
#if !DEBUG
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
#endif
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        this.blacklist = JsonConvert.DeserializeObject<List<string>>(json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"There was an error parsing the {BLACKLIST_FILE} file.");
#if DEBUG
                        Console.WriteLine(ex);
#else
                        Console.WriteLine(ex.Message);
#endif
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

    public AssetsService(bool useLocalBlacklist)
    {
        this.useLocalBlacklist = useLocalBlacklist;
    }

    public static void BackupSaves(string directory, string backupLocation)
    {
        BackupDirectory(directory, "Saves", backupLocation);
    }

    public static void BackupMemories(string directory, string backupLocation)
    {
        BackupDirectory(directory, "Memories", backupLocation);
    }

    private static void BackupDirectory(string rootDirectory, string folderName, string backupLocation)
    {
        if (string.IsNullOrEmpty(rootDirectory))
        {
            throw new ArgumentNullException(nameof(rootDirectory));
        }

        if (string.IsNullOrEmpty(backupLocation))
        {
            throw new ArgumentNullException(nameof(backupLocation));
        }

        Console.WriteLine($"Compressing and backing up {folderName} directory...");

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
                Console.WriteLine("Complete.");
            }
            else
            {
                Console.WriteLine($"Backup with the same contents already exists, skipping...");
            }
        }
        else
        {
            Console.WriteLine($"No {folderName} directory found, skipping backup...");
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

    public static void PruneSaveStates(string rootDirectory, string coreName = null)
    {
        BackupMemories(ServiceHelper.UpdateDirectory, ServiceHelper.SettingsService.GetConfig().backup_saves_location);
        string savesPath = Path.Combine(rootDirectory, "Memories", "Save States");

        //YYYYMMDD_HHMMSS_SOMETHING_SOMETHING_GAMETITLE.STA
        string pattern = @"^(\d\d\d\d\d\d\d\d_\d\d\d\d\d\d)_[A-Za-z]+_[A-Za-z0-9]+_(.*)\.sta$";
        Regex regex = new Regex(pattern);

        foreach (var dir in Directory.EnumerateDirectories(savesPath) )
        {
            //just skip it if it's not the requested core
            if (coreName != null && dir != Path.Combine(savesPath, coreName))
            {
                continue;
            }

            // Dictionary to store the most recent file per game
            var mostRecentFiles = new Dictionary<string, (string fileName, long timestamp)>();

            // Get all .sta files in the directory
            var files = Directory.GetFiles(dir, "*.sta");

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                // Match the filename with the regex
                Match match = regex.Match(fileName);

                if (match.Success)
                {
                    // Extract the timestamp (group 1) and game name (group 2)
                    long timestamp = long.Parse(match.Groups[1].Value.Replace("_", String.Empty));
                    string applicationName = match.Groups[2].Value;

                    // Check if this game already has a file in the dictionary
                    if (!mostRecentFiles.ContainsKey(applicationName) || mostRecentFiles[applicationName].timestamp < timestamp)
                    {
                        // If the file is more recent, or the first one for this game, store it
                        mostRecentFiles[applicationName] = (fileName, timestamp);
                    }
                }
            }

            // Now prune the folder by deleting older files
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                // Match the filename with the regex
                Match match = regex.Match(fileName);

                if (match.Success)
                {
                    // Extract the game name (Group 2)
                    string applicationName = match.Groups[2].Value;

                    // Check if the file is the most recent for this game
                    if (mostRecentFiles.ContainsKey(applicationName) && mostRecentFiles[applicationName].fileName != fileName)
                    {
                        // If it's not the most recent file for this game, delete it
                        File.Delete(file);
                        Console.WriteLine($"Deleted file: {fileName}");
                    }
                }
            }
        }

        Console.WriteLine("Pruning completed. Most recent save state for each game retained.");
    }
}
