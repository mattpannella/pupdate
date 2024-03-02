using System.Runtime.InteropServices;
using Pannella.Helpers;
using Pannella.Models.Extras;
using Pannella.Services;
using GithubRelease = Pannella.Models.Github.Release;

namespace Pannella;

internal partial class Program
{
    // return true if newer version is available
    private static bool CheckVersion(string path)
    {
        try
        {
            List<GithubRelease> releases = GithubApiService.GetReleases(USER, REPOSITORY,
                ServiceHelper.SettingsService.GetConfig().github_token);

            string tagName = releases[0].tag_name;
            string v = SemverUtil.FindSemver(tagName);

            if (v != null)
            {
                bool check = SemverUtil.SemverCompare(v, VERSION);

                if (check)
                {
                    Console.WriteLine($"A new version {v} is available. Downloading now...");

                    string url = string.Format(RELEASE_URL, tagName, SYSTEM_OS_PLATFORM);
                    string saveLocation = Path.Combine(path, "pupdate.zip");

                    HttpHelper.Instance.DownloadFile(url, saveLocation);

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
#if DEBUG
            Console.WriteLine(e);
#else
            Console.WriteLine(e.Message);
#endif
            return false;
        }
    }

    private static void PrintPocketExtraInfo(PocketExtra extra)
    {
        Console.WriteLine(extra.id);
        Console.WriteLine(string.IsNullOrEmpty(extra.name) // name is not required for additional assets
            ? $"  {extra.core_identifiers[0]}"
            : $"  {extra.name}");
        Console.WriteLine(Util.WordWrap(extra.description, 80, "    "));
        Console.WriteLine($"    More info: https://github.com/{extra.github_user}/{extra.github_repository}");

        foreach (var additionalLink in extra.additional_links)
        {
            Console.WriteLine($"                {additionalLink}");
        }

        Console.WriteLine();
    }

    private static void FunFacts()
    {
        if (ServiceHelper.CoresService.InstalledCores.Count == 0)
        {
            return;
        }

        string[] sleepSupported = ServiceHelper.CoresService.InstalledCores
            .Where(c => ServiceHelper.CoresService.ReadCoreJson(c.identifier).framework.sleep_supported)
            .Select(c => c.identifier)
            .ToArray();

        if (sleepSupported.Any())
        {
            string list = string.Join(", ", sleepSupported);
            string wrapped = Util.WordWrap(list, 75, "    ");

            // Console.WriteLine();
            Console.WriteLine("Fun fact! The ONLY cores that support save states and sleep are the following:");
            Console.WriteLine(wrapped);
            Console.WriteLine("Please don't bother the developers of the other cores about this feature. It's a lot\nof work and most likely will not be coming.");
        }
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
        Console.WriteLine("Press any key to continue.");
        Console.ReadKey(true);
    }
}
