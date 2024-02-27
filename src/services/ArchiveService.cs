using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Archive;
using Pannella.Models.Settings;
using File = Pannella.Models.Archive.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public class ArchiveService
{
    private const string METADATA = "https://archive.org/metadata/{0}";

    public const string DOWNLOAD = "https://archive.org/download/{0}/{1}";

    private readonly string archiveName;
    private readonly string gnwArchiveName;
    private readonly CustomArchive customArchive;
    private readonly bool crcCheck;

    public ArchiveService(string archiveName, string gnwArchiveName, bool crcCheck)
    {
        this.archiveName = archiveName;
        this.gnwArchiveName = gnwArchiveName;
        this.customArchive = null;
        this.crcCheck = crcCheck;
    }

    public ArchiveService(CustomArchive customArchive, string gnwArchiveName, bool crcCheck)
    {
        this.customArchive = customArchive;
        this.archiveName = string.Empty;
        this.gnwArchiveName = gnwArchiveName;
        this.crcCheck = crcCheck;
    }

    private Archive archiveFiles;

    public Archive ArchiveFiles
    {
        get
        {
            if (this.archiveFiles == null)
            {
                Console.WriteLine("Loading Assets Index...");

                if (this.customArchive != null)
                {
                    Uri baseUrl = new Uri(this.customArchive.url);
                    Uri url = new Uri(baseUrl, this.customArchive.index);

                    this.archiveFiles = ArchiveService.GetFilesCustom(url.ToString());
                }
                else
                {
                    this.archiveFiles = ArchiveService.GetFiles(this.archiveName);
                }
            }

            return this.archiveFiles;
        }
    }

    private Archive gameAndWatchArchiveFiles;

    public Archive GameAndWatchArchiveFiles
    {
        get
        {
            if (this.gameAndWatchArchiveFiles == null)
            {
                Console.WriteLine("Loading Game and Watch Assets Index...");

                if (this.gnwArchiveName != this.archiveName)
                {
                    this.gameAndWatchArchiveFiles = ArchiveService.GetFiles(this.gnwArchiveName);

                    // remove the metadata files since we're processing the entire json list
                    this.gameAndWatchArchiveFiles.files.RemoveAll(file =>
                        Path.GetExtension(file.name) is ".sqlite" or ".torrent" or ".xml");
                }
                else
                {
                    // there are GNW files in the openFPGA-files archive as well as the archive maintained by Espiox
                    // if the GNW archive is set to the openFPGA-files archive, create a second archive
                    // with just the GNW files from it so things behave correctly
                    this.gameAndWatchArchiveFiles = new Archive
                    {
                        item_last_updated = this.ArchiveFiles.item_last_updated,
                        files = this.ArchiveFiles.files.Where(file => file.name.EndsWith(".gnw")).ToList()
                    };

                    this.gameAndWatchArchiveFiles.files_count = this.gameAndWatchArchiveFiles.files.Count;
                }
            }

            return this.gameAndWatchArchiveFiles;
        }
    }

    private static Archive GetFiles(string archive)
    {
        string url = string.Format(METADATA, archive);
        string json = HttpHelper.Instance.GetHTML(url);
        Archive result = JsonSerializer.Deserialize<Archive>(json);

        return result;
    }

    private static Archive GetFilesCustom(string url)
    {
        try
        {
            string json = HttpHelper.Instance.GetHTML(url);
            Archive result = JsonSerializer.Deserialize<Archive>(json);

            return result;
        }
        catch
        {
            return null;
        }
    }

    public File GetArchiveFile(string fileName)
    {
        File file = this.ArchiveFiles.files.FirstOrDefault(file => file.name == fileName);

        return file;
    }

    public void DownloadArchiveFile(string archiveName, File archiveFile, string destination)
    {
        try
        {
            string url = string.Format(DOWNLOAD, archiveName, archiveFile.name);
            string destinationFileName = Path.Combine(destination, archiveFile.name);
            int count = 0;

            do
            {
                Console.WriteLine($"Downloading '{archiveFile.name}'");
                HttpHelper.Instance.DownloadFile(url, destinationFileName, 600);
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

    private bool ValidateChecksum(string filePath, File archiveFile)
    {
        if (!this.crcCheck)
            return true;

        if (archiveFile == null)
            return true;

        if (Util.CompareChecksum(filePath, archiveFile.crc32))
            return true;

        Console.WriteLine($"Bad checksum for {Path.GetFileName(filePath)}");
        return false;
    }
}
