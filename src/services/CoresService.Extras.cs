using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.Events;
using Pannella.Models.Extras;
using Pannella.Models.Github;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using File = System.IO.File;

namespace Pannella.Services;

public partial class CoresService
{
    private const string POCKET_EXTRAS_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/pocket_extras.json";
    private const string POCKET_EXTRAS_FILE = "pocket_extras.json";

    private List<PocketExtra> pocketExtrasList;

    public List<PocketExtra> PocketExtrasList
    {
        get
        {
            if (pocketExtrasList == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.GetConfig().use_local_pocket_extras,
                    POCKET_EXTRAS_FILE,
                    POCKET_EXTRAS_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var pocketExtras = JsonConvert.DeserializeObject<PocketExtras>(json);

                        pocketExtrasList = pocketExtras.pocket_extras;
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {POCKET_EXTRAS_FILE} file.");
#if DEBUG
                        WriteMessage(ex.ToString());
#else
                        WriteMessage(ex.Message);
#endif
                    }
                }
                else
                {
                    pocketExtrasList = new List<PocketExtra>();
                }
            }

            return pocketExtrasList;
        }
    }

    public PocketExtra GetPocketExtra(string pocketExtraIdOrCoreIdentifier)
    {
        return this.PocketExtrasList.Find(e =>
            e.id == pocketExtraIdOrCoreIdentifier ||
            e.core_identifiers.Any(x => x == pocketExtraIdOrCoreIdentifier));
    }

    public void GetPocketExtra(PocketExtra pocketExtra, string path, bool downloadAssets)
    {
        switch (pocketExtra.type)
        {
            case PocketExtraType.additional_assets:
                DownloadPocketExtras(pocketExtra, path, downloadAssets);
                break;

            case PocketExtraType.combination_platform:
            case PocketExtraType.variant_core:
                DownloadPocketExtrasPlatform(pocketExtra, path, downloadAssets);
                break;
        }

        WriteMessage(string.Concat(
            Environment.NewLine,
            $"Please go to https://www.github.com/{pocketExtra.github_user}/{pocketExtra.github_repository}",
            Environment.NewLine,
            "  for more information and to support the author of the Extra.",
            Environment.NewLine
        ));
    }

    private void DownloadPocketExtrasPlatform(PocketExtra pocketExtra, string path, bool downloadAssets)
    {
        Release release = GithubApiService.GetLatestRelease(pocketExtra.github_user, pocketExtra.github_repository,
            this.settingsService.GetConfig().github_token);
        Asset asset = release.assets.FirstOrDefault(x => x.name.StartsWith(pocketExtra.github_asset_prefix));

        if (asset == null)
        {
            WriteMessage($"GitHub asset for '{pocketExtra.name}' was not found.");
            return;
        }

        string localFile = Path.Combine(ServiceHelper.TempDirectory, asset.name);
        string extractPath = Path.Combine(ServiceHelper.TempDirectory, "temp");

        try
        {
            WriteMessage($"Downloading asset '{asset.name}'...");
            HttpHelper.Instance.DownloadFile(asset.browser_download_url, localFile);
            WriteMessage("Download complete.");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipHelper.ExtractToDirectory(localFile, extractPath);
            File.Delete(localFile);

            if (pocketExtra.has_placeholders)
            {
                var placeFiles = Directory.GetFiles(extractPath, "PLACE_*", SearchOption.AllDirectories);
                var renameFiles = Directory.GetFiles(extractPath, "RENAME_*", SearchOption.AllDirectories);

                if (placeFiles.Length > 0)
                {
                    WriteMessage("Downloading core file placeholders...");

                    foreach (var placeFile in placeFiles)
                    {
                        string contents = File.ReadAllText(placeFile);
                        Uri uri = new Uri(contents);
                        string placeFileName = Path.GetFileName(uri.LocalPath);
                        string localPlaceFileName = Path.Combine(Path.GetDirectoryName(placeFile)!, placeFileName);

                        WriteMessage($"Downloading: '{placeFileName}'");
                        HttpHelper.Instance.DownloadFile(uri.ToString(), localPlaceFileName);
                        WriteMessage("Download complete.");

                        File.Delete(placeFile);
                    }
                }
                else if (renameFiles.Length > 0)
                {
                    WriteMessage("Downloading & renaming core file placeholders...");

                    foreach (var renameFile in renameFiles)
                    {
                        string renameFileName = Path.GetFileNameWithoutExtension(renameFile);
                        //           111111111122222222223333
                        // 0123456789012345678901234567890123
                        // RENAME_BITSTREAM_TO_DEFENDER_RBF_R
                        int extensionIndex = renameFileName.LastIndexOf("_RBF_R", StringComparison.InvariantCulture);
                        string renamedFileName = renameFileName.Substring(20, extensionIndex - 20).ToLowerInvariant() + ".rbf_r";

                        string contents = File.ReadAllText(renameFile);
                        Uri uri = new Uri(contents);
                        string localRenameFileName = Path.Combine(Path.GetDirectoryName(renameFile)!, renamedFileName);

                        WriteMessage($"Downloading '{renamedFileName}'");
                        HttpHelper.Instance.DownloadFile(uri.ToString(), localRenameFileName);
                        WriteMessage("Download complete.");

                        File.Delete(renameFile);
                    }
                }
                else
                {
                    throw new FileNotFoundException("Core RBF_R file locators not found.");
                }
            }

            string destinationAssetsMra = Path.Combine(extractPath, "Assets", pocketExtra.id, "mra");

            if (Directory.Exists(destinationAssetsMra))
                Directory.Delete(destinationAssetsMra, true);

            WriteMessage("Installing...");
            Util.CopyDirectory(extractPath, path, true, true);

            WriteMessage("Complete.");
        }
        catch (Exception e)
        {
            WriteMessage("Something happened while trying to install the asset files...");
#if DEBUG
            WriteMessage(e.ToString());
#else
            WriteMessage(e.Message);
#endif
            return;
        }

        WriteMessage("Downloading assets...");

        foreach (var coreDirectory in Directory.GetDirectories(Path.Combine(extractPath, "Cores")))
        {
            string coreIdentifier = Path.GetFileName(coreDirectory);
            Core core = this.GetCore(coreIdentifier);

            this.settingsService.EnableCore(core.identifier, true, release.tag_name);

            if (downloadAssets)
            {
                WriteMessage($"\n{coreIdentifier}");

                var results = this.DownloadAssets(core);

                UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
                {
                    Message = "Complete.",
                    InstalledAssets = (List<string>)results["installed"],
                    SkippedAssets = (List<string>)results["skipped"],
                    MissingLicenses = (bool)results["missingLicense"]
                        ? new List<string> { core.identifier }
                        : new List<string>(),
                    SkipOutro = true,
                };

                OnUpdateProcessComplete(args);
            }
        }

        this.settingsService.Save();
        Directory.Delete(extractPath, true);
    }

    private void DownloadPocketExtras(PocketExtra pocketExtra, string path, bool downloadAssets)
    {
        var core = this.GetCore(pocketExtra.core_identifiers[0]);

        if (!this.IsInstalled(core.identifier))
        {
            bool jtBetaKeyExists = this.ExtractJTBetaKey();

            WriteMessage($"The '{pocketExtra.core_identifiers[0]}' core is not currently installed.");

            if (core.requires_license && !jtBetaKeyExists)
            {
                // Moved message to the ExtractBetaKey method
                return;
            }

            WriteMessage("Would you like to install it? [Y]es, [N]o");

            bool? result = null;

            while (result == null)
            {
                result = Console.ReadKey(true).Key switch
                {
                    ConsoleKey.Y => true,
                    ConsoleKey.N => false,
                    _ => null
                };
            }

            if (!result.Value)
                return;

            this.Install(core);

            if (core.requires_license)
            {
                this.CopyLicense(core);
            }

            if (!this.IsInstalled(core.identifier))
            {
                // WriteMessage("The core still isn't installed.");
                return;
            }

            this.settingsService.EnableCore(core.identifier);
        }

        Release release = GithubApiService.GetLatestRelease(pocketExtra.github_user, pocketExtra.github_repository,
            this.settingsService.GetConfig().github_token);
        Asset asset = release.assets.FirstOrDefault(x => x.name.StartsWith(pocketExtra.github_asset_prefix));

        if (asset == null)
        {
            WriteMessage($"GitHub asset for '{pocketExtra.core_identifiers[0]}' was not found.");
            return;
        }

        string localFile = Path.Combine(path, asset.name);
        string extractPath = Path.Combine(path, "temp");
        string sourceAssetsCore = Path.Combine(extractPath, "Assets", core.platform_id);
        string destinationAssetsCore = Path.Combine(path, "Assets", core.platform_id);

        try
        {
            WriteMessage($"Downloading asset '{asset.name}'...");
            HttpHelper.Instance.DownloadFile(asset.browser_download_url, localFile);
            WriteMessage("Download complete.");
            WriteMessage("Installing...");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipHelper.ExtractToDirectory(localFile, extractPath);
            File.Delete(localFile);
            Util.CopyDirectory(sourceAssetsCore, destinationAssetsCore, true, true);

            string destinationAssetsMra = Path.Combine(path, "Assets", core.platform_id, "mra");

            if (Directory.Exists(destinationAssetsMra))
                Directory.Delete(destinationAssetsMra, true);

            if (core.identifier.StartsWith("jotego"))
            {
                string sourcePresetsCore = Path.Combine(extractPath, "Presets", core.identifier);
                string destinationPresetsCore = Path.Combine(path, "Presets", core.identifier);

                Util.CopyDirectory(sourcePresetsCore, destinationPresetsCore, true, true);
            }
            else
            {
                string sourceDataJson = Path.Combine(extractPath, "Cores", core.identifier, "data.json");
                string destinationDataJson = Path.Combine(path, "Cores", core.identifier, "data.json");
                string destinationDataJsonBackup = Path.Combine(path, "Cores", core.identifier,
                    $"data.{DateTime.Now:yyyy-MM-dd_HH.mm.ss}.json");

                File.Copy(destinationDataJson, destinationDataJsonBackup, true);
                File.Copy(sourceDataJson, destinationDataJson, true);
            }

            Directory.Delete(extractPath, true);
            WriteMessage("Complete.");
        }
        catch (Exception e)
        {
            WriteMessage("Something happened while trying to install the asset files...");
#if DEBUG
            WriteMessage(e.ToString());
#else
            WriteMessage(e.Message);
#endif
            return;
        }

        if (downloadAssets)
        {
            WriteMessage("Downloading assets...");

            var results = this.DownloadAssets(core);

            UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
            {
                Message = "Complete.",
                InstalledAssets = (List<string>)results["installed"],
                SkippedAssets = (List<string>)results["skipped"],
                MissingLicenses = (bool)results["missingLicense"]
                    ? new List<string> { core.identifier }
                    : new List<string>(),
                SkipOutro = true,
            };

            OnUpdateProcessComplete(args);
        }

        this.settingsService.EnableCore(core.identifier, true, release.tag_name);
        this.settingsService.Save();
    }

    public string GetMostRecentRelease(PocketExtra pocketExtra)
    {
        Release release = GithubApiService.GetLatestRelease(pocketExtra.github_user, pocketExtra.github_repository,
            this.settingsService.GetConfig().github_token);

        return release.tag_name;
    }
}
