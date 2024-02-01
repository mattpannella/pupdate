using System.IO.Compression;
using System.Text.Json;
using Pannella.Helpers;

namespace Pannella.Services;

public static class AssetsService
{
    private const string BLACKLIST = "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";

    public static void BackupSaves(string directory, string backupLocation)
    {
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentNullException(nameof(directory));
        }

        if (string.IsNullOrEmpty(backupLocation))
        {
            throw new ArgumentNullException(nameof(backupLocation));
        }

        Console.WriteLine("Compressing and backing up Saves directory...");
        string savesPath = Path.Combine(directory, "Saves");
        string fileName = $"Saves_Backup_{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.zip";
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
            Console.WriteLine("No Saves directory found, skipping backup...");
        }
    }

    public static async Task<string[]> GetBlacklist()
    {
        string json = await HttpHelper.Instance.GetHTML(BLACKLIST);
        string[] files = JsonSerializer.Deserialize<string[]>(json);

        return files ?? Array.Empty<string>();
    }
}
