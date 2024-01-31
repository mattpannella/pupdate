using System.IO.Compression;
using System.Text.Json;

namespace pannella.analoguepocket;

public static class AssetsService
{
    private const string IMAGE_PACKS = "https://raw.githubusercontent.com/mattpannella/pupdate/main/image_packs.json";
    private const string BLACKLIST = "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";

    public static void BackupSaves(string directory, string backupLocation)
    {
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentNullException(nameof(directory));

        if (string.IsNullOrEmpty(backupLocation))
            throw new ArgumentNullException(nameof(backupLocation));
        
        Console.WriteLine("Compressing and backing up Saves directory...");
        string savesPath = Path.Combine(directory, "Saves");
        string fileName = $"Saves_Backup_{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.zip";
        string archiveName = Path.Combine(backupLocation, fileName);

        if (Directory.Exists(savesPath))
        {
            if (!Directory.Exists(backupLocation))
                Directory.CreateDirectory(backupLocation);
            
            ZipFile.CreateFromDirectory(savesPath, archiveName);
            Console.WriteLine("Complete.");
        }
        else
        {
            Console.WriteLine("No Saves directory found, skipping backup...");
        }
    }
    
    public static async Task<ImagePack[]> GetImagePacks()
    {
        string json = await Factory.GetHttpHelper().GetHTML(IMAGE_PACKS);
        ImagePack[] packs = JsonSerializer.Deserialize<ImagePack[]?>(json);

        if(packs != null) {
            return packs;
        }

        return new ImagePack[0];
    }

    public static async Task<string[]> GetBlacklist()
    {
        string json = await Factory.GetHttpHelper().GetHTML(BLACKLIST);
        string[] files = JsonSerializer.Deserialize<string[]?>(json);

        if(files != null) {
            return files;
        }

        return new string[0];
    }
}