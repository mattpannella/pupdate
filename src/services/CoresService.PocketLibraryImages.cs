using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Models.PocketLibraryImages;
using ArchiveFile = Pannella.Models.Archive.File;
using SettingsArchive = Pannella.Models.Settings.Archive;
using File = System.IO.File;

namespace Pannella.Services;

public partial class CoresService
{
    private const string POCKET_LIBRARY_IMAGES_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/pocket_library_images.json";
    private const string POCKET_LIBRARY_IMAGES_FILE = "pocket_library_images.json";
    private const string LIBRARY_IMAGE_SET_ARCHIVE_FILE = "Library_Image_Set_v1.0.zip";
    private const string POCKET_LIBRARY_IMAGES_EXTRACT_PATH_SEGMENT = "temp";

    private List<PocketLibraryImageMenu> pocketLibraryImagesList;

    public List<PocketLibraryImageMenu> PocketLibraryImagesList
    {
        get
        {
            if (pocketLibraryImagesList == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.Config.use_local_pocket_library_images,
                    POCKET_LIBRARY_IMAGES_FILE,
                    POCKET_LIBRARY_IMAGES_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var pocketLibraryImages = JsonConvert.DeserializeObject<PocketLibraryImages>(json);

                        pocketLibraryImagesList = pocketLibraryImages.pocket_library_images;
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {POCKET_LIBRARY_IMAGES_FILE} file.");
                        WriteMessage(this.settingsService.Debug.show_stack_traces
                            ? ex.ToString()
                            : Util.GetExceptionMessage(ex));
                    }
                }
                else
                {
                    pocketLibraryImagesList = new List<PocketLibraryImageMenu>();
                }
            }

            return pocketLibraryImagesList;
        }
    }

    public PocketLibraryImage GetPocketLibraryImage(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        string trimmed = id.Trim();

        foreach (PocketLibraryImageMenu menu in this.PocketLibraryImagesList)
        {
            if (menu?.entries == null)
                continue;

            PocketLibraryImage found = menu.entries.Find(e =>
                string.Equals(e.id, trimmed, StringComparison.OrdinalIgnoreCase));

            if (found != null)
                return found;
        }

        return null;
    }

    public void DownloadPockLibraryImages()
    {
        SettingsArchive archive = this.archiveService.GetArchive();
        ArchiveFile archiveFile = this.archiveService.GetArchiveFile(LIBRARY_IMAGE_SET_ARCHIVE_FILE);

        if (archiveFile != null)
        {
            string localFile = Path.Combine(ServiceHelper.TempDirectory, LIBRARY_IMAGE_SET_ARCHIVE_FILE);
            string extractPath = Path.Combine(ServiceHelper.TempDirectory, POCKET_LIBRARY_IMAGES_EXTRACT_PATH_SEGMENT);

            try
            {
                WriteMessage("Downloading library images...");
                this.archiveService.DownloadArchiveFile(archive, archiveFile, ServiceHelper.TempDirectory);
                WriteMessage("Installing library images...");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipHelper.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);
                Util.CopyDirectory(extractPath, this.installPath, true, true);

                Directory.Delete(extractPath, true);
                WriteMessage("Complete.");
            }
            catch (Exception ex)
            {
                WriteMessage("Something happened while trying to install the asset files...");
                WriteMessage(this.settingsService.Debug.show_stack_traces
                    ? ex.ToString()
                    : Util.GetExceptionMessage(ex));
            }
        }
        else
        {
            WriteMessage("Pocket Library Images not found in the archive.");
        }
    }

    public void DownloadPocketLibraryImages(PocketLibraryImage image)
    {
        if (image?.sources == null || image.sources.Count == 0)
            return;

        string owner = image.github_user.Trim();
        string githubRepository = image.github_repository.Trim();
        string extractPath = Path.Combine(ServiceHelper.TempDirectory, POCKET_LIBRARY_IMAGES_EXTRACT_PATH_SEGMENT);

        try
        {
            Release release = GithubApiService.GetLatestRelease(owner, githubRepository, this.settingsService.Config.github_token);
            List<Asset> assets = release?.assets ?? new List<Asset>();

            int totalCopied = 0;

            foreach (PocketLibraryImageSource src in image.sources)
            {
                string assetName = src.release_asset.Trim();
                Asset asset = assets.FirstOrDefault(a => string.Equals(a.name, assetName, StringComparison.OrdinalIgnoreCase));

                if (asset?.browser_download_url == null)
                {
                    WriteMessage($"Release asset '{assetName}' not found in {owner}/{githubRepository}.");
                    return;
                }

                string localFile = Path.Combine(ServiceHelper.TempDirectory, asset.name);

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                WriteMessage($"Downloading {asset.name}...");
                HttpHelper.Instance.DownloadFile(asset.browser_download_url, localFile);
                WriteMessage($"Extracting {asset.name}...");
                ZipHelper.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);

                string pathUnder = (src.path_under_extract ?? string.Empty).Trim().TrimStart('/', '\\');
                string srcDir = pathUnder.Length == 0 || pathUnder == "."
                    ? extractPath
                    : Path.Combine(extractPath, pathUnder.Replace('/', Path.DirectorySeparatorChar));
                string destFolder = src.dest_images_subfolder.Trim();
                string destDir = Path.Combine(this.installPath, "System", "Library", "Images", destFolder);

                if (!Directory.Exists(srcDir))
                {
                    WriteMessage($"Warning: folder '{pathUnder}' not found in {asset.name} — skipping this source.");
                    continue;
                }

                totalCopied += Util.CopyDirectory(srcDir, destDir, recursive: false, overwrite: true);
            }

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            WriteMessage("Complete.");

            if (totalCopied > 0 && !string.IsNullOrWhiteSpace(image.post_install_note))
            {
                WriteMessage(string.Empty);
                WriteMessage(Util.WordWrap(image.post_install_note.Trim(), 80, string.Empty));
            }
        }
        catch (Exception ex)
        {
            WriteMessage("Something went wrong while installing pocket library images from GitHub...");
            WriteMessage(this.settingsService.Debug.show_stack_traces
                ? ex.ToString()
                : Util.GetExceptionMessage(ex));
        }
    }
}
