using System.Diagnostics;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Services;
using File = System.IO.File;
using GithubAsset = Pannella.Models.Github.Asset;

namespace Pannella;

internal partial class Program
{
    private static void BuildGameAndWatchRoms()
    {
        Release release = GithubApiService.GetLatestRelease("agg23", "fpga-gameandwatch",
            ServiceHelper.SettingsService.GetConfig().github_token);

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
                Exec($"chmod +x {execLocation}");
                break;

            default:
                execLocation = Path.Combine(execLocation, "linux", execName);
                manifestPath = Path.Combine(manifestPath, "linux", "manifest.json");
                Exec($"chmod +x {execLocation}");
                break;
        }

        string romLocation = Path.Combine(ServiceHelper.UpdateDirectory, "Assets", "gameandwatch", "agg23.GameAndWatch");
        string outputLocation = Path.Combine(ServiceHelper.UpdateDirectory, "Assets", "gameandwatch", "common");

        try
        {
            // Execute
            Console.WriteLine($"Executing {execLocation}");

            ProcessStartInfo pInfo = new ProcessStartInfo(execLocation)
            {
                Arguments = $"--mame-path \"{romLocation}\" --output-path \"{outputLocation}\" --manifest-path \"{manifestPath}\" supported",
                UseShellExecute = false
            };

            Process p = Process.Start(pInfo);

            p!.WaitForExit();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"An error occurred: {e.GetType().Name} : {e}");
        }
    }

    private static void Exec(string cmd)
    {
        var escapedArgs = cmd.Replace("\"", "\\\"");

        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "/bin/bash",
            Arguments = $"-c \"{escapedArgs}\""
        };

        process.Start();
        process.WaitForExit();
    }
}
