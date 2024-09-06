using System.Net;
using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;
using SettingsArchive = Pannella.Models.Settings.Archive;
using ArchiveFile = Pannella.Models.Archive.File;
using Archive = Pannella.Models.Archive.Archive;

namespace Pannella.Services;

public class ArchiveService : Base
{
    private const string METADATA = "https://archive.org/metadata/{0}";
    private const string DOWNLOAD = "https://archive.org/download/{0}/{1}";

    private readonly bool crcCheck;
    private readonly Dictionary<string, Archive> archiveFiles;
    private readonly List<SettingsArchive> archives;
    private readonly bool useCustomArchive;

    public ArchiveService(List<SettingsArchive> archives, bool crcCheck, bool useCustomArchive)
    {
        this.crcCheck = crcCheck;
        this.useCustomArchive = useCustomArchive;
        this.archives = archives;
        this.archiveFiles = new Dictionary<string, Archive>();
    }

    public SettingsArchive GetArchive(string coreIdentifier = null)
    {
        SettingsArchive result = null;

        if (!string.IsNullOrEmpty(coreIdentifier))
        {
            result = this.archives.FirstOrDefault(x => x.name == coreIdentifier);
        }

        if (result == null)
        {
            coreIdentifier = this.useCustomArchive ? "custom" : "default";
            result = this.archives.FirstOrDefault(x => x.name == coreIdentifier);
        }

        return result;
    }

    public ArchiveFile GetArchiveFile(string fileName, string coreIdentifier = null)
    {
        var files = this.GetArchiveFiles(coreIdentifier);

        return files.FirstOrDefault(x => x.name == fileName);
    }

    public IEnumerable<ArchiveFile> GetArchiveFiles(string coreIdentifier)
    {
        SettingsArchive archive = this.GetArchive(coreIdentifier);

        return this.GetArchiveFiles(archive);
    }

    public IEnumerable<ArchiveFile> GetArchiveFiles(SettingsArchive archive)
    {
        bool found = this.archiveFiles.TryGetValue(archive.archive_name, out Archive internetArchive);

        if (!found)
        {
            WriteMessage($"Loading Assets Index for '{archive.archive_name}'...");

            if (useCustomArchive && archive.type != ArchiveType.core_specific_archive)
            {
                Uri baseUrl = new Uri(archive.url);
                Uri url = new Uri(baseUrl, archive.index);

                internetArchive = ArchiveService.GetFilesCustom(url.ToString());
            }
            else
            {
                internetArchive = ArchiveService.GetFiles(archive.archive_name);
            }

            this.archiveFiles.Add(archive.archive_name, internetArchive);
        }

        if (archive.file_extensions is { Count: > 0 })
        {
            var filtered = internetArchive.files.Where(x => archive.file_extensions.Any(y =>
                string.Equals(y, Path.GetExtension(x.name), StringComparison.InvariantCultureIgnoreCase))).ToList();

            return filtered;
        }

        return internetArchive.files;
    }

    private static Archive GetFiles(string archive)
    {
        string url = string.Format(METADATA, archive);
        string json = HttpHelper.Instance.GetHTML(url);
        Archive result = JsonConvert.DeserializeObject<Archive>(json);

        return result;
    }

    private static Archive GetFilesCustom(string url)
    {
        try
        {
            string json = HttpHelper.Instance.GetHTML(url);
            Archive result = JsonConvert.DeserializeObject<Archive>(json);

            return result;
        }
        catch
        {
            return null;
        }
    }

    public bool DownloadArchiveFile(SettingsArchive archive, ArchiveFile archiveFile, string destination)
    {
        if (archive == null || archiveFile == null)
            return false;

        try
        {
            string url;

            if (archive.type == ArchiveType.custom_archive)
            {
                Uri baseUrl = new Uri(archive.url);
                Uri uri = new Uri(baseUrl, archiveFile.name);

                url = uri.ToString();
            }
            else
            {
                url = string.Format(DOWNLOAD, archive.archive_name, archiveFile.name);
            }

            int count = 0;
            string destinationFileName = Path.Combine(destination, archiveFile.name);
            string subDirectory = Path.GetDirectoryName(archiveFile.name);

            if (!string.IsNullOrEmpty(subDirectory))
            {
                string destinationDirectory = Path.Combine(destination, subDirectory);

                Directory.CreateDirectory(Path.Combine(destinationDirectory));
            }

            do
            {
                HttpHelper.Instance.DownloadFile(url, destinationFileName, 600);
                count++;
            }
            while (count < 3 && !ValidateChecksum(destinationFileName, archiveFile));
        }
        catch (HttpRequestException e)
        {
            WriteMessage(e.StatusCode switch
            {
                HttpStatusCode.NotFound => $"Unable to find '{archiveFile.name}' in archive '{archive.name}'",
                _ => $"There was a problem downloading '{archiveFile.name}'"
            });

            // throw;

            return false;
        }
        catch (Exception e)
        {
            WriteMessage($"Something went wrong with '{archiveFile.name}'");
#if DEBUG
            WriteMessage(e.ToString());
#else
            WriteMessage(e.Message);
#endif
            return false;
        }

        return true;
    }

    private bool ValidateChecksum(string filePath, ArchiveFile archiveFile)
    {
        if (!this.crcCheck)
            return true;

        if (archiveFile == null)
            return true;

        if (Util.CompareChecksum(filePath, archiveFile.crc32))
            return true;

        WriteMessage($"Bad checksum for {Path.GetFileName(filePath)}");
        return false;
    }
}
