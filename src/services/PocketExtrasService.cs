using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Extras;
using Pannella.Models.Github;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using File = System.IO.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public class PocketExtrasService : BaseProcess
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/pocket_extras.json";

    public static List<PocketExtra> GetPocketExtrasList()
    {
#if DEBUG
        string json = File.ReadAllText("pocket_extras.json");
#else
        string json = GlobalHelper.SettingsManager.GetConfig().use_local_pocket_extras
            ? File.ReadAllText("pocket_extras.json")
            : HttpHelper.Instance.GetHTML(END_POINT);
#endif
        PocketExtras files = JsonSerializer.Deserialize<PocketExtras>(json);

        return files.pocket_extras;
    }

    private void DownloadPocketExtrasPlatform(string user, string repository, string platformName,
        string assetName, string path, bool downloadAssets, bool skipPlaceholderFiles, bool refreshLocalCores)
    {
        Release release = GithubApiService.GetLatestRelease(user, repository);
        Asset asset = release.assets.FirstOrDefault(x => x.name.StartsWith(assetName));

        if (asset == null)
        {
            WriteMessage($"Pocket Extras asset for the '{platformName}' core was not found.");
            return;
        }

        string localFile = Path.Combine(path, asset.name);
        string extractPath = Path.Combine(path, "temp");

        try
        {
            WriteMessage($"Downloading asset '{asset.name}'...");
            HttpHelper.Instance.DownloadFile(asset.browser_download_url, localFile);
            WriteMessage("Download complete.");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipFile.ExtractToDirectory(localFile, extractPath);
            File.Delete(localFile);

            if (!skipPlaceholderFiles)
            {
                var placeFiles = Directory.GetFiles(extractPath, "PLACE_*", SearchOption.AllDirectories);

                if (!placeFiles.Any())
                    throw new FileNotFoundException("Core RBF_R file locators not found.");

                WriteMessage("Downloading core file placeholders...");

                foreach (var placeFile in placeFiles)
                {
                    string contents = File.ReadAllText(placeFile);
                    Uri uri = new Uri(contents);
                    string placeFileName = Path.GetFileName(uri.LocalPath);
                    string localPlaceFileName = Path.Combine(Path.GetDirectoryName(placeFile)!, placeFileName);

                    WriteMessage($"Downloading '{placeFileName}'");
                    HttpHelper.Instance.DownloadFile(uri.ToString(), localPlaceFileName);
                    WriteMessage("Download complete.");

                    File.Delete(placeFile);
                }
            }

            string destinationAssetsMra = Path.Combine(extractPath, "Assets", platformName, "mra");

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

        if (refreshLocalCores)
            GlobalHelper.RefreshLocalCores();

        //coreUpdater.RefreshStatusUpdater();

        foreach (var coreDirectory in Directory.GetDirectories(Path.Combine(extractPath, "Cores")))
        {
            string coreIdentifier = Path.GetFileName(coreDirectory);
            Core core = GlobalHelper.GetCore(coreIdentifier);

            if (!core.IsStatusUpdatedRegistered())
            {
                core.StatusUpdated += this.core_StatusUpdated;
            }

            GlobalHelper.SettingsManager.EnableCore(core.identifier, true, release.tag_name);

            if (downloadAssets)
            {
                WriteMessage($"\n{coreIdentifier}");

                var results = core.DownloadAssets();

                UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
                {
                    Message = "Complete.",
                    InstalledAssets = (List<string>)results["installed"],
                    SkippedAssets = (List<string>)results["skipped"],
                    MissingBetaKeys = (bool)results["missingBetaKey"]
                        ? new List<string> { core.identifier }
                        : new List<string>(),
                    SkipOutro = true,
                };

                OnUpdateProcessComplete(args);
            }
        }

        GlobalHelper.SettingsManager.SaveSettings();
        Directory.Delete(extractPath, true);

        //WriteMessage("Complete.");
    }

    private void DownloadPocketExtras(string user, string repository, string coreIdentifier, string assetName,
        string path, bool downloadAssets)
    {
        var core = GlobalHelper.GetCore(coreIdentifier);

        if (!core.IsInstalled())
        {
            WriteMessage($"The '{coreIdentifier}' core is not currently installed.");
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

            core.Install(GlobalHelper.SettingsManager.GetConfig().preserve_platforms_folder);

            if (!core.IsInstalled())
            {
                // WriteMessage("The core still isn't installed.");
                return;
            }
        }

        Release release = GithubApiService.GetLatestRelease(user, repository);
        Asset asset = release.assets.FirstOrDefault(x => x.name.StartsWith(assetName));

        if (asset == null)
        {
            WriteMessage($"Pocket Extras asset for the '{coreIdentifier}' core was not found.");
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

            ZipFile.ExtractToDirectory(localFile, extractPath);
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

            var results = core.DownloadAssets();

            UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
            {
                Message = "Complete.",
                InstalledAssets = (List<string>)results["installed"],
                SkippedAssets = (List<string>)results["skipped"],
                MissingBetaKeys = (bool)results["missingBetaKey"]
                    ? new List<string> { core.identifier }
                    : new List<string>(),
                SkipOutro = true,
            };

            OnUpdateProcessComplete(args);
        }

        GlobalHelper.SettingsManager.EnableCore(core.identifier, true, release.tag_name);
        GlobalHelper.SettingsManager.SaveSettings();
    }

    public void GetPocketExtra(PocketExtra pocketExtra, string path, bool downloadAssets, bool refreshLocalCores)
    {
        switch (pocketExtra.type)
        {
            case PocketExtraType.additional_assets:
                DownloadPocketExtras(pocketExtra.github_user, pocketExtra.github_repository,
                    pocketExtra.core_identifiers[0], pocketExtra.github_asset_prefix,
                    path, downloadAssets);
                break;

            case PocketExtraType.combination_platform:
            case PocketExtraType.variant_core:
                DownloadPocketExtrasPlatform(pocketExtra.github_user, pocketExtra.github_repository,
                    pocketExtra.platform_name, pocketExtra.github_asset_prefix, path, downloadAssets,
                    !pocketExtra.has_placeholders, refreshLocalCores);
                break;
        }

        WriteMessage($"{Environment.NewLine}Please go to https://www.github.com/{pocketExtra.github_user}/{pocketExtra.github_repository} for more information and to support the author of the Extra.");
    }

    public static string GetMostRecentRelease(PocketExtra pocketExtra)
    {
        Release release = GithubApiService.GetLatestRelease(pocketExtra.github_user, pocketExtra.github_repository);

        return release.tag_name;
    }

    private void core_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }
}
