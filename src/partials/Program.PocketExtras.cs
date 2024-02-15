using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Github;
using Pannella.Models.Settings;
using Pannella.Services;
using File = System.IO.File;

namespace Pannella;

internal partial class Program
{
    private static async Task DownloadPocketExtrasPlatform(string user, string repository, string platformName,
        string assetName, string path, PocketCoreUpdater coreUpdater, bool skipPlaceholderFiles = false)
    {
        Release release = await GithubApiService.GetLatestRelease(user, repository);
        Asset asset = release.assets.FirstOrDefault(x => x.name.StartsWith(assetName));

        if (asset == null)
        {
            Console.WriteLine($"Pocket Extras asset for the '{platformName}' core was not found.");
            return;
        }

        string localFile = Path.Combine(path, asset.name);
        string extractPath = Path.Combine(path, "temp");

        try
        {
            Console.WriteLine($"Downloading asset '{asset.name}'...");
            await HttpHelper.Instance.DownloadFileAsync(asset.browser_download_url, localFile);
            Console.WriteLine("Download complete.");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipFile.ExtractToDirectory(localFile, extractPath);
            File.Delete(localFile);

            if (!skipPlaceholderFiles)
            {
                var placeFiles = Directory.GetFiles(extractPath, "PLACE_*", SearchOption.AllDirectories);

                if (!placeFiles.Any())
                    throw new FileNotFoundException("Core RBF_R file locators not found.");

                Console.WriteLine("Downloading core file placeholders...");

                foreach (var placeFile in placeFiles)
                {
                    string contents = await File.ReadAllTextAsync(placeFile);
                    Uri uri = new Uri(contents);
                    string placeFileName = Path.GetFileName(uri.LocalPath);
                    string localPlaceFileName = Path.Combine(Path.GetDirectoryName(placeFile)!, placeFileName);

                    Console.WriteLine($"Downloading '{placeFileName}'");
                    await HttpHelper.Instance.DownloadFileAsync(uri.ToString(), localPlaceFileName);

                    File.Delete(placeFile);
                }
            }

            string destinationAssetsMra = Path.Combine(extractPath, "Assets", platformName, "mra");

            if (Directory.Exists(destinationAssetsMra))
                Directory.Delete(destinationAssetsMra, true);

            Console.WriteLine("Download complete.");
            Console.WriteLine("Installing...");
            Util.CopyDirectory(extractPath, path, true, true);
            Console.WriteLine("Complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Something happened while trying to install the asset files...");
            Console.WriteLine(ex);
            return;
        }

        Console.WriteLine("Downloading assets...");
        GlobalHelper.RefreshLocalCores();
        coreUpdater.RefreshStatusUpdater();

        foreach (var coreDirectory in Directory.GetDirectories(Path.Combine(extractPath, "Cores")))
        {
            string coreIdentifier = Path.GetFileName(coreDirectory);
            Core core = GlobalHelper.GetCore(coreIdentifier);
            CoreSettings coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(core.identifier);

            coreSettings.skip = false;
            coreSettings.pocket_extras = true;

            // should I call await core.DownloadAssets here instead?
            await coreUpdater.RunAssetDownloader(coreIdentifier, true);
        }

        GlobalHelper.SettingsManager.SaveSettings();
        Directory.Delete(extractPath, true);

        Console.WriteLine("Complete.");
    }

    private static async Task DownloadPocketExtras(string user, string repository, string coreIdentifier,
        string assetName, string path, PocketCoreUpdater coreUpdater)
    {
        var core = GlobalHelper.GetCore(coreIdentifier);

        if (!core.IsInstalled())
        {
            Console.WriteLine($"The '{coreIdentifier}' core is not currently installed.");
            Console.WriteLine("Would you like to install it? [Y]es, [N]o");

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
            await coreUpdater.RunUpdates(coreIdentifier, skipOutro: true);

            if (!core.IsInstalled())
            {
                // Console.WriteLine("The core still isn't installed.");
                return;
            }
        }

        Release release = await GithubApiService.GetLatestRelease(user, repository);
        Asset asset = release.assets.FirstOrDefault(x => x.name.StartsWith(assetName));

        if (asset == null)
        {
            Console.WriteLine($"Pocket Extras asset for the '{coreIdentifier}' core was not found.");
            return;
        }

        string localFile = Path.Combine(path, asset.name);
        string extractPath = Path.Combine(path, "temp");
        string sourceAssetsCore = Path.Combine(extractPath, "Assets", core.platform_id);
        string destinationAssetsCore = Path.Combine(path, "Assets", core.platform_id);

        try
        {
            Console.WriteLine($"Downloading asset '{asset.name}'...");
            await HttpHelper.Instance.DownloadFileAsync(asset.browser_download_url, localFile);
            Console.WriteLine("Download complete.");
            Console.WriteLine("Installing...");

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
            Console.WriteLine("Complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Something happened while trying to install the asset files...");
            Console.WriteLine(ex);
            return;
        }

        Console.WriteLine("Downloading assets...");
        // should I call await core.DownloadAssets here instead?
        await coreUpdater.RunAssetDownloader(core.identifier, true);
        Console.WriteLine("Complete.");

        CoreSettings coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(core.identifier);

        coreSettings.skip = false;
        coreSettings.pocket_extras = true;

        GlobalHelper.SettingsManager.SaveSettings();

        // TODO: Modify 'Update All' and 'Update {core}' to check the pocket_extras flag and act accordingly when true.
    }

    private static async Task DownloadDonkeyKongPocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "ericlewis.DonkeyKong",
            "pocket-extras-dk", path, coreUpdater);
    }

    private static async Task DownloadRadarScopePocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "ericlewis.RadarScope",
            "pocket-extras-dk", path, coreUpdater);
    }

    private static async Task DownloadBubbleBobblePocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "jotego.jtbubl",
            "pocket-extras-jotego", path, coreUpdater);
    }

    private static async Task DownloadCapcomCps1PocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "jotego.jtcps1",
            "pocket-extras-jotego", path, coreUpdater);
    }

    private static async Task DownloadCapcomCps15PocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "jotego.jtcps15",
            "pocket-extras-jotego", path, coreUpdater);
    }

    private static async Task DownloadCapcomCps2PocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "jotego.jtcps2",
            "pocket-extras-jotego", path, coreUpdater);
    }

    private static async Task DownloadPangPocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtras("dyreschlock", "pocket-extras", "jotego.jtpang",
            "pocket-extras-jotego", path, coreUpdater);
    }

    private static async Task DownloadToaplan2cPocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtrasPlatform("dyreschlock", "pocket-extras","toaplan2_c",
            "pocket-extras-toaplan2_c", path, coreUpdater);
    }

    private static async Task DownloadSegaSystem16cPocketExtras(string path, PocketCoreUpdater coreUpdater)
    {
        await DownloadPocketExtrasPlatform("espiox", "jts16_complete", "jts16_c",
            "jts16_complete", path, coreUpdater, true);
    }
}
