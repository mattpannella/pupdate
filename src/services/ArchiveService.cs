using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Archive;
using File = Pannella.Models.Archive.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public static class ArchiveService
{
    private const string METADATA = "https://archive.org/metadata/{0}";

    private const string DOWNLOAD = "https://archive.org/download/{0}/{1}";

    public static async Task<Archive> GetFiles(string archive)
    {
        string url = string.Format(METADATA, archive);
        string json = await HttpHelper.Instance.GetHTML(url);
        Archive result = JsonSerializer.Deserialize<Archive>(json);

        return result;
    }

    public static async Task<Archive> GetFilesCustom(string url)
    {
        try
        {
            string json = await HttpHelper.Instance.GetHTML(url);
            Archive result = JsonSerializer.Deserialize<Archive>(json);

            return result;
        }
        catch
        {
            return null;
        }
    }

    public static async Task DownloadArchiveFile(string archiveName, File archiveFile, string destination)
    {
        try
        {
            string url = string.Format(DOWNLOAD, archiveName, archiveFile.name);
            string destinationFileName = Path.Combine(destination, archiveFile.name);
            int count = 0;

            do
            {
                Console.WriteLine($"Downloading '{archiveFile.name}'");
                await HttpHelper.Instance.DownloadFileAsync(url, destinationFileName, 600);
                Console.WriteLine($"Finished downloading '{archiveFile.name}'");
                count++;
            }
            while (count < 3 && !ValidateChecksum(destinationFileName, archiveFile));
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.StatusCode switch
            {
                HttpStatusCode.NotFound => $"Unable to find '{archiveFile.name}' in archive '{archiveName}'",
                _ => $"There was a problem downloading '{archiveFile.name}'"
            });
            throw;
        }
    }

    private static bool ValidateChecksum(string filePath, File archiveFile)
    {
        if (!GlobalHelper.SettingsManager.GetConfig().crc_check)
            return true;

        if (archiveFile == null)
            return true;

        if (Util.CompareChecksum(filePath, archiveFile.crc32))
            return true;

        Console.WriteLine($"Bad checksum for {Path.GetFileName(filePath)}");
        return false;
    }
}
