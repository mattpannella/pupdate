using System.IO.Compression;
using Newtonsoft.Json;
using Pannella.Helpers;

namespace Pannella.Services;

public class AssetsService
{
    private const string BLACKLIST = "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";

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
        string fileName = $"{folderName}_Backup_{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.zip";
        string archiveName = Path.Combine(backupLocation, fileName);

        if (Directory.Exists(savesPath))
        {
            if (!Directory.Exists(backupLocation))
            {
                Directory.CreateDirectory(backupLocation);
            }

            ZipFile.CreateFromDirectory(savesPath, archiveName);
            Console.WriteLine("Complete.");
        }
        else
        {
            Console.WriteLine($"No {folderName} directory found, skipping backup...");
        }
    }
}
