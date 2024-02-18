using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Extras;
using Pannella.Models.Github;
using File = System.IO.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public class PocketExtrasService : BaseService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/pocket_extras.json";

    public static async Task<List<PocketExtra>> GetPocketExtrasList()
    {
#if DEBUG
        string json = await File.ReadAllTextAsync("pocket_extras.json");
#else
        string json = await HttpHelper.Instance.GetHTML(END_POINT);
#endif
        PocketExtras files = JsonSerializer.Deserialize<PocketExtras>(json);

        return files.pocket_extras;
    }

    private async Task DownloadPocketExtrasPlatform(string user, string repository, string platformName,
        string assetName, string path, bool downloadAssets, bool skipPlaceholderFiles, bool refreshLocalCores)
    {
        Release release = await GithubApiService.GetLatestRelease(user, repository);
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
            await HttpHelper.Instance.DownloadFileAsync(asset.browser_download_url, localFile);
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
                    string contents = await File.ReadAllTextAsync(placeFile);
                    Uri uri = new Uri(contents);
                    string placeFileName = Path.GetFileName(uri.LocalPath);
                    string localPlaceFileName = Path.Combine(Path.GetDirectoryName(placeFile)!, placeFileName);

                    WriteMessage($"Downloading '{placeFileName}'");
                    await HttpHelper.Instance.DownloadFileAsync(uri.ToString(), localPlaceFileName);
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
        catch (Exception ex)
        {
            WriteMessage("Something happened while trying to install the asset files...");
            WriteMessage(ex.ToString());
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

            core.StatusUpdated += this.core_StatusUpdated;

            // CoreSettings coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(core.identifier);
            //
            // coreSettings.skip = false;
            // coreSettings.pocket_extras = true;
            // coreSettings.pocket_extras_version = release.tag_name;
            GlobalHelper.SettingsManager.EnableCore(core.identifier, true, release.tag_name);

            if (downloadAssets)
            {
                // should I call await core.DownloadAssets here instead?
                //await coreUpdater.RunAssetDownloader(coreIdentifier, true);
                WriteMessage($"\n{coreIdentifier}");
                var results = await core.DownloadAssets();

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

    private async Task DownloadPocketExtras(string user, string repository, string coreIdentifier, string assetName,
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

            // should I call core.Install here instead?
            //await coreUpdater.RunUpdates(coreIdentifier, skipOutro: true);
            await core.Install(GlobalHelper.SettingsManager.GetConfig().preserve_platforms_folder);

            if (!core.IsInstalled())
            {
                // WriteMessage("The core still isn't installed.");
                return;
            }
        }

        Release release = await GithubApiService.GetLatestRelease(user, repository);
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
            await HttpHelper.Instance.DownloadFileAsync(asset.browser_download_url, localFile);
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
        catch (Exception ex)
        {
            WriteMessage("Something happened while trying to install the asset files...");
            WriteMessage(ex.ToString());
            return;
        }

        if (downloadAssets)
        {
            WriteMessage("Downloading assets...");
            // should I call await core.DownloadAssets here instead?
            //await coreUpdater.RunAssetDownloader(core.identifier, true);
            var results = await core.DownloadAssets();

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
            //WriteMessage("Complete.");
        }

        // CoreSettings coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(core.identifier);
        //
        // coreSettings.skip = false;
        // coreSettings.pocket_extras = true;
        // coreSettings.pocket_extras_version = release.tag_name;

        GlobalHelper.SettingsManager.EnableCore(core.identifier, true, release.tag_name);
        GlobalHelper.SettingsManager.SaveSettings();
    }

    public async Task GetPocketExtra(PocketExtra pocketExtra, string path, bool downloadAssets, bool refreshLocalCores)
    {
        switch (pocketExtra.type)
        {
            case PocketExtraType.additional_assets:
                await DownloadPocketExtras(pocketExtra.github_user, pocketExtra.github_repository,
                    pocketExtra.core_identifiers[0], pocketExtra.github_asset_prefix,
                    path, downloadAssets);
                break;

            case PocketExtraType.combination_platform:
            case PocketExtraType.variant_core:
                await DownloadPocketExtrasPlatform(pocketExtra.github_user, pocketExtra.github_repository,
                    pocketExtra.platform_name, pocketExtra.github_asset_prefix, path, downloadAssets,
                    !pocketExtra.has_placeholders, refreshLocalCores);
                break;
        }
    }

    public async Task<string> GetMostRecentRelease(PocketExtra pocketExtra)
    {
        Release release = await GithubApiService.GetLatestRelease(pocketExtra.github_user, pocketExtra.github_repository);

        return release.tag_name;
    }

    private void core_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }
}
