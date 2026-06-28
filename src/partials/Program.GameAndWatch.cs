using System.Diagnostics;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Services;
using File = System.IO.File;
using GithubAsset = Pannella.Models.Github.Asset;

namespace Pannella;

internal static partial class Program
{
    // log defaults to Console.WriteLine for the classic menu; the TUI passes its status sink. The
    // generator child process also has its stdout/stderr redirected through log so it streams to the
    // status pane instead of writing straight to the terminal (which would corrupt the TUI canvas).
    internal static void BuildGameAndWatchRoms(Action<string> log = null)
    {
        log ??= Console.WriteLine;

        Release release = GithubApiService.GetLatestRelease("agg23", "fpga-gameandwatch",
            ServiceHelper.SettingsService.Config.github_token);

        foreach (GithubAsset asset in release.assets)
        {
            if (asset.name.EndsWith("Tools.zip"))
            {
                string downloadPath = Path.Combine(ServiceHelper.UpdateDirectory, "tools", "gameandwatch");
                string filename = Path.Combine(downloadPath, asset.name);

                if (!File.Exists(filename))
                {
                    Directory.CreateDirectory(downloadPath);
                    HttpHelper.Instance.DownloadFile(asset.browser_download_url, filename);
                    ZipHelper.ExtractToDirectory(filename, downloadPath, true);
                }

                break;
            }
        }

        string execName = "fpga-gnw-romgenerator";
        string execLocation = Path.Combine(ServiceHelper.UpdateDirectory, "tools", "gameandwatch");
        string manifestPath = Path.Combine(ServiceHelper.UpdateDirectory, "tools", "gameandwatch");

        switch (SYSTEM_OS_PLATFORM)
        {
            case "win":
                execName += ".exe";
                execLocation = Path.Combine(execLocation, "windows", execName);
                manifestPath = Path.Combine(manifestPath, "windows", "manifest.json");
                break;

            case "mac":
                execLocation = Path.Combine(execLocation, "mac", execName);
                manifestPath = Path.Combine(manifestPath, "mac", "manifest.json");
                Util.MakeExecutable(execLocation);
                break;

            default:
                execLocation = Path.Combine(execLocation, "linux", execName);
                manifestPath = Path.Combine(manifestPath, "linux", "manifest.json");
                Util.MakeExecutable(execLocation);
                break;
        }

        string romLocation = Path.Combine(ServiceHelper.UpdateDirectory, "Assets", "gameandwatch", "agg23.GameAndWatch");
        string outputLocation = Path.Combine(ServiceHelper.UpdateDirectory, "Assets", "gameandwatch", "common");

        try
        {
            // Execute
            log($"Executing {execLocation}");

            ProcessStartInfo pInfo = new ProcessStartInfo(execLocation)
            {
                Arguments = $"--mame-path \"{romLocation}\" --output-path \"{outputLocation}\" --manifest-path \"{manifestPath}\" supported",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process p = Process.Start(pInfo);

            // Stream the generator's output through log rather than letting it write to the terminal
            // directly (which would corrupt the TUI canvas).
            p!.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.WaitForExit();
        }
        catch (Exception ex)
        {
            log($"An error occurred: {ex.GetType().Name} : {ex}");
        }
    }
}
