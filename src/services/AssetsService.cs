using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Pannella.Helpers;

namespace Pannella.Services;

public class AssetsService
{
    private const string BLACKLIST =
        "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";

    private readonly bool useLocalBlacklist;
    private List<string> blacklist;

    public List<string> Blacklist
    {
        get
        {
            if (this.blacklist == null)
            {
#if DEBUG
                string json = File.ReadAllText("blacklist.json");
#else
                string json = useLocalBlacklist
                    ? File.ReadAllText("blacklist.json")
                    : HttpHelper.Instance.GetHTML(BLACKLIST);
#endif
                this.blacklist = JsonConvert.DeserializeObject<List<string>>(json);
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

    public static void BackupDirectory(
        string rootDirectory,
        string folderName,
        string backupLocation
    )
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
            string fileName = $"{folderName}_Backup_{dateStamp}-version{truncatedHash}.zip";
            string archiveName = Path.Combine(backupLocation, fileName);

            if (!Directory.Exists(backupLocation))
            {
                Directory.CreateDirectory(backupLocation);
            }

            bool isDuplicateBackup = Directory
                .GetFiles(backupLocation, $"{folderName}_Backup_*-version{truncatedHash}.zip")
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
        using (var sha256 = SHA256.Create())
        {
            var allFiles = Directory
                .GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();

            var hashBuilder = new StringBuilder();

            foreach (var filePath in allFiles)
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                byte[] hashBytes = sha256.ComputeHash(fileBytes);

                hashBuilder.Append(
                    BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()
                );
            }

            byte[] finalHashBytes = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(hashBuilder.ToString())
            );
            return BitConverter.ToString(finalHashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
