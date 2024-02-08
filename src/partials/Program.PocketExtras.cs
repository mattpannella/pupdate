using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Services;
using File = System.IO.File;

namespace Pannella;

internal partial class Program
{
    private static async Task DownloadPocketExtras(string coreIdentifier, string assetName, string path, PocketCoreUpdater coreUpdater)
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

            await coreUpdater.RunUpdates(coreIdentifier);
        }

        Release release = await GithubApiService.GetLatestRelease("dyreschlock", "pocket-extras");
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
        string destinationAssetsMra = Path.Combine(path, "Assets", "mra");
        string sourceDataJson = Path.Combine(extractPath, "Cores", core.identifier, "data.json");
        string destinationDataJson = Path.Combine(path, "Cores", core.identifier, "data.json");
        string destinationDataJsonBackup = Path.Combine(path, "Cores", core.identifier, "data.backup.json");

        try
        {
            Console.WriteLine($"Downloading asset '{asset.name}'...");
            await HttpHelper.Instance.DownloadFileAsync(asset.browser_download_url, localFile);
            Console.WriteLine("Download complete.");
            Console.WriteLine("Installing...");
            ZipFile.ExtractToDirectory(localFile, extractPath);
            File.Delete(localFile);
            Util.CopyDirectory(sourceAssetsCore, destinationAssetsCore, true, true);
            Directory.Delete(destinationAssetsMra, true);

            if (!File.Exists(destinationDataJsonBackup))
                File.Copy(destinationDataJson, destinationDataJsonBackup, true);

            File.Copy(sourceDataJson, destinationDataJson, true);
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
        await coreUpdater.RunAssetDownloader(core.identifier, true);
        Console.WriteLine("Complete.");

        GlobalHelper.SettingsManager.GetCoreSettings(core.identifier).pocket_extras = true;
        GlobalHelper.SettingsManager.SaveSettings();

        // TODO: Modify 'Update All' and 'Update {core}' to check the pocket_extras flag and act accordingly when true.
        // TODO: Provide uninstall capability for the pocket_extras additions
        // TODO: Put all the pocket_extras menu items into their own submenu

        // TODO: During core uninstall, ask if roms and saves should also be deleted.
    }
}
