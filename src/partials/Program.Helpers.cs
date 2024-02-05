using System.Runtime.InteropServices;
using Pannella.Helpers;
using Pannella.Services;
using GithubRelease = Pannella.Models.Github.Release;

namespace Pannella;

internal partial class Program
{
    // return true if newer version is available
    private static async Task<bool> CheckVersion(string path)
    {
        try
        {
            List<GithubRelease> releases = await GithubApiService.GetReleases(USER, REPOSITORY);

            string tag_name = releases[0].tag_name;
            string v = SemverUtil.FindSemver(tag_name);

            if (v != null)
            {
                bool check = SemverUtil.SemverCompare(v, VERSION);

                if (check)
                {
                    Console.WriteLine("A new version is available. Downloading now...");

                    string url = string.Format(RELEASE_URL, tag_name, SYSTEM_OS_PLATFORM);
                    string saveLocation = Path.Combine(path, "pupdate.zip");

                    await HttpHelper.Instance.DownloadFileAsync(url, saveLocation);

                    Console.WriteLine("Download complete.");
                    Console.WriteLine(saveLocation);
                    Console.WriteLine("Go to " + releases[0].html_url + " for a change log");
                }
                else
                {
                    Console.WriteLine("Up to date.");
                }

                return check;
            }

            return false;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private static void FunFacts()
    {
        if (GlobalHelper.InstalledCores.Count == 0)
        {
            return;
        }

        string[] sleepSupported = GlobalHelper.InstalledCores
            .Where(c => c.GetConfig().framework.sleep_supported)
            .Select(c => c.identifier)
            .ToArray();
        string list = string.Join(", ", sleepSupported);

        Console.WriteLine();
        Console.WriteLine("Fun fact! The ONLY cores that support save states and sleep are the following:");
        Console.WriteLine(list);
        Console.WriteLine("Please don't bother the developers of the other cores about this feature. It's a lot of work and most likely will not be coming.");
    }

    private static string GetSystemPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "mac";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Architecture arch = RuntimeInformation.ProcessArchitecture;

            return arch switch
            {
                Architecture.Arm64 => "linux_arm64",
                Architecture.Arm => "linux_arm32",
                _ => "linux"
            };
        }

        return string.Empty;
    }

    private static void Pause()
    {
        if (!CLI_MODE)
        {
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey(true);
        }
    }

    private static void PauseExit(int exitCode = 0)
    {
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey(true); // wait for input so the console doesn't auto close in windows
        Environment.Exit(exitCode);
    }
}
