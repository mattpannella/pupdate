using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Services;
using File = System.IO.File;

namespace Pannella;

internal static partial class Program
{
    private static void DownloadGameBoyPalettes()
    {
        Release release = GithubApiService.GetLatestRelease("davewongillies", "openfpga-palettes",
            ServiceHelper.SettingsService.Config.github_token);
        Asset asset = release.assets.FirstOrDefault(a => a.name.EndsWith(".zip"));

        if (asset != null)
        {
            string localFile = Path.Combine(ServiceHelper.TempDirectory, asset.name);
            string extractPath = Path.Combine(ServiceHelper.TempDirectory, "temp");

            try
            {
                Console.WriteLine($"Downloading asset '{asset.name}'...");
                HttpHelper.Instance.DownloadFile(asset.browser_download_url, localFile);
                Console.WriteLine("Download complete.");
                Console.WriteLine("Installing...");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);
                Util.CopyDirectory(extractPath, ServiceHelper.UpdateDirectory, true, true);

                Directory.Delete(extractPath, true);
                Console.WriteLine("Complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something happened while trying to install the asset files...");
                Console.WriteLine(ServiceHelper.SettingsService.Debug.show_stack_traces
                    ? ex
                    : Util.GetExceptionMessage(ex));
            }
        }
    }
}
