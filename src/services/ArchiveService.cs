using System.Net;
using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;
using SettingsArchive = Pannella.Models.Settings.Archive;
using ArchiveFile = Pannella.Models.Archive.File;
using Archive = Pannella.Models.Archive.Archive;
using System.Linq.Expressions;

namespace Pannella.Services;

public class ArchiveService : Base
{
    private const string METADATA = "https://archive.org/metadata/{0}";
    private const string DOWNLOAD = "https://archive.org/download/{0}/{1}";
    private const string LOGIN = "https://archive.org/services/account/login/";

    private readonly bool crcCheck;
    private readonly Dictionary<string, Archive> archiveFiles;
    private readonly List<SettingsArchive> archives;
    private readonly bool useCustomArchive;
    private readonly InternetArchive credentials;
    private readonly bool showStackTraces;
    private readonly bool cacheArchiveFiles;
    private readonly string cacheDirectory;

    public ArchiveService(List<SettingsArchive> archives, InternetArchive credentials, bool crcCheck, bool useCustomArchive, bool showStackTraces, bool cacheArchiveFiles, string cacheDirectory)
    {
        this.crcCheck = crcCheck;
        this.useCustomArchive = useCustomArchive;
        this.archives = archives;
        this.archiveFiles = new Dictionary<string, Archive>();
        this.credentials = credentials;
        this.showStackTraces = showStackTraces;
        this.cacheArchiveFiles = cacheArchiveFiles;
        this.cacheDirectory = cacheDirectory;
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

    // ReSharper disable once MemberCanBePrivate.Global
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

            if ((useCustomArchive && archive.type != ArchiveType.core_specific_archive) || archive.type == ArchiveType.core_specific_custom_archive)
            {
                string url = $"{archive.url.TrimEnd('/')}/{archive.index.TrimStart('/')}";

                internetArchive = ArchiveService.GetFilesCustom(url);
            }
            else
            {
                internetArchive = ArchiveService.GetFiles(archive.archive_name);
            }
            if (internetArchive == null) {
                WriteMessage($"Unable to load Assets Index for '{archive.archive_name}'");
                internetArchive = new Archive();
                internetArchive.files = new List<ArchiveFile>(); //empty list
            }

            this.archiveFiles.Add(archive.archive_name, internetArchive);
        }

        if (archive.file_extensions is { Count: > 0 })
        {
            var filtered = internetArchive.files.Where(x => archive.file_extensions.Any(y =>
                string.Equals(y, Path.GetExtension(x.name), StringComparison.InvariantCultureIgnoreCase))).ToList();

            return filtered;
        }

        if (archive.files is { Count: > 0 })
        {
            var filtered = internetArchive.files.Where(x => archive.files.Any(y =>
                string.Equals(y, Path.GetFileName(x.name), StringComparison.InvariantCultureIgnoreCase))).ToList();

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

            if (archive.type is ArchiveType.custom_archive or ArchiveType.core_specific_custom_archive)
            {
                url = $"{archive.url.TrimEnd('/')}/{archiveFile.name.TrimStart('/')}";
            }
            else
            {
                this.Authenticate();
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

            // Cache hit check
            if (this.cacheArchiveFiles && !string.IsNullOrEmpty(archiveFile.md5))
            {
                string cacheFilePath = GetCacheFilePath(archive, archiveFile);

                if (File.Exists(cacheFilePath) &&
                    Util.CompareChecksum(cacheFilePath, archiveFile.md5, Util.HashTypes.MD5))
                {
                    WriteMessage($"Cache hit for '{archiveFile.name}', copying from cache...");
                    try
                    {
                        File.Copy(cacheFilePath, destinationFileName, overwrite: true);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"Cache copy failed for '{archiveFile.name}', falling through to download.");
                        WriteMessage(this.showStackTraces ? ex.ToString() : Util.GetExceptionMessage(ex));
                    }
                }
            }

            do
            {
                HttpHelper.Instance.DownloadFile(url, destinationFileName, 600);
                count++;
            } while (count < 3 && !ValidateChecksum(destinationFileName, archiveFile));

            // Populate cache after successful download
            if (this.cacheArchiveFiles && !string.IsNullOrEmpty(archiveFile.md5))
            {
                try
                {
                    string cacheFilePath = GetCacheFilePath(archive, archiveFile);
                    string cacheSubDir = Path.GetDirectoryName(cacheFilePath);

                    if (!string.IsNullOrEmpty(cacheSubDir))
                    {
                        Directory.CreateDirectory(cacheSubDir);
                    }

                    File.Copy(destinationFileName, cacheFilePath, overwrite: true);
                }
                catch (Exception ex)
                {
                    WriteMessage($"Failed to cache '{archiveFile.name}', continuing.");
                    WriteMessage(this.showStackTraces ? ex.ToString() : Util.GetExceptionMessage(ex));
                }
            }

            // if (File.Exists(destinationFileName) && Path.GetExtension(destinationFileName) == ".zip")
            // {
            //     //extract
            //     ZipHelper.ExtractToDirectory(destinationFileName, Path.GetDirectoryName(destinationFileName), true);
            //     //delete
            //     File.Delete(destinationFileName);
            // }
            // else if (File.Exists(destinationFileName) && Path.GetExtension(destinationFileName) == ".7z")
            // {
            //     //extract
            //     SevenZipHelper.ExtractToDirectory(destinationFileName, Path.GetDirectoryName(destinationFileName));
            //     //delete
            //     File.Delete(destinationFileName);
            // }
        }
        catch (HttpRequestException ex)
        {
            WriteMessage(ex.StatusCode switch
            {
                HttpStatusCode.NotFound => $"Unable to find '{archiveFile.name}' in archive '{archive.name}'",
                _ => $"There was a problem downloading '{archiveFile.name}'"
            });

            // throw;

            return false;
        }
        catch (Exception ex)
        {
            WriteMessage($"Something went wrong with '{archiveFile.name}'");
            WriteMessage(this.showStackTraces
                ? ex.ToString()
                : Util.GetExceptionMessage(ex));

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

    private void Authenticate()
    {
        if (this.credentials != null)
        {
            var fields = new Dictionary<string, string>
            {
                { "login", "true" },
                { "remember", "true" },
                { "submit_by_js", "true" },
                { "referrer", "https://archive.org/CREATE/" }
            };

            HttpHelper.Instance.GetAuthCookie(this.credentials.username, this.credentials.password, LOGIN, fields);
        }
    }

    private string GetCacheFilePath(SettingsArchive archive, ArchiveFile archiveFile)
    {
        return Path.Combine(this.cacheDirectory, archive.archive_name, archiveFile.name);
    }
}
