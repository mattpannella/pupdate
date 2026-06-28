using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Services;
using File = System.IO.File;

namespace Pannella;

internal static partial class Program
{
    // log defaults to Console.WriteLine for the classic menu; the TUI passes its status sink so
    // output streams into the status pane instead of corrupting the canvas.
    internal static void DownloadGameBoyPalettes(Action<string> log = null)
    {
        log ??= Console.WriteLine;

        Release release = GithubApiService.GetLatestRelease("davewongillies", "openfpga-palettes",
            ServiceHelper.SettingsService.Config.github_token);
        Asset asset = release.assets.FirstOrDefault(a => a.name.EndsWith(".zip"));

        if (asset != null)
        {
            string localFile = Path.Combine(ServiceHelper.TempDirectory, asset.name);
            string extractPath = Path.Combine(ServiceHelper.TempDirectory, "temp");

            try
            {
                log($"Downloading asset '{asset.name}'...");
                HttpHelper.Instance.DownloadFile(asset.browser_download_url, localFile);
                log("Download complete.");
                log("Installing...");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);
                Util.CopyDirectory(extractPath, ServiceHelper.UpdateDirectory, true, true);

                Directory.Delete(extractPath, true);
                log("Complete.");
            }
            catch (Exception ex)
            {
                log("Something happened while trying to install the asset files...");
                log(ServiceHelper.SettingsService.Debug.show_stack_traces
                    ? ex.ToString()
                    : Util.GetExceptionMessage(ex));
            }
        }
    }
}
