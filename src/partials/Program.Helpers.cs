using System.Runtime.InteropServices;
using Pannella.Helpers;
using Pannella.Models.Extras;
using Pannella.Models.PocketLibraryImages;
using Pannella.Services;
using GithubRelease = Pannella.Models.Github.Release;

namespace Pannella;

internal static partial class Program
{
    // return true if newer version is available
    private static bool CheckVersion(string path)
    {
        try
        {
            List<GithubRelease> releases = GithubApiService.GetReleases(USER, REPOSITORY,
                ServiceHelper.SettingsService.Config.github_token);

#if NET7_0
            // legacy build stays on its own major (notify-only, links to GitHub)
            int major = new Version(VERSION).Major;
            GithubRelease target = SelectLatestRelease(releases, major, major);
#else
            // modern build tracks the newest release overall (moves users to 5.x+)
            GithubRelease target = SelectLatestRelease(releases, 0, int.MaxValue);
#endif

            if (target == null)
            {
                Console.WriteLine("Up to date.");

                return false;
            }

            string tagName = target.tag_name;
            string v = SemverUtil.FindSemver(tagName);

            if (v != null)
            {
                bool check = SemverUtil.SemverCompare(v, VERSION);

                if (check)
                {
#if NET7_0
                    Console.WriteLine($"A new version {v} is available.");
#else
                    Console.WriteLine($"A new version {v} is available. Downloading now...");

                    string url = string.Format(RELEASE_URL, tagName, SYSTEM_OS_PLATFORM);
                    string saveLocation = Path.Combine(path, "pupdate.zip");

                    HttpHelper.Instance.DownloadFile(url, saveLocation);

                    Console.WriteLine("Download complete.");
                    Console.WriteLine(saveLocation);
#endif
                    Console.WriteLine("Go to " + target.html_url + " for a change log.");
                }
                else
                {
                    Console.WriteLine("Up to date.");
                }

                return check;
            }

            return false;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(ServiceHelper.SettingsService.Debug.show_stack_traces
                ? ex
                : Util.GetExceptionMessage(ex));

            return false;
        }
    }

    // Highest-semver non-draft release with a major in [minMajor, maxMajor], or null if none.
    internal static GithubRelease SelectLatestRelease(List<GithubRelease> releases, int minMajor, int maxMajor)
    {
        GithubRelease best = null;
        Version bestVersion = null;

        foreach (GithubRelease release in releases)
        {
            if (release.draft)
                continue;

            string semver = SemverUtil.FindSemver(release.tag_name);

            if (semver == null || !Version.TryParse(semver, out Version version))
                continue;

            if (version.Major < minMajor || version.Major > maxMajor)
                continue;

            if (bestVersion == null || version > bestVersion)
            {
                best = release;
                bestVersion = version;
            }
        }

        return best;
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

    private static void PrintPocketLibraryImageInfo(PocketLibraryImage image)
    {
        Console.WriteLine(image.id);
        Console.WriteLine($"  {image.menu_label ?? image.id}");

        if (!string.IsNullOrWhiteSpace(image.description))
            Console.WriteLine(Util.WordWrap(image.description, 80, "    "));

        if (!string.IsNullOrWhiteSpace(image.github_user) && !string.IsNullOrWhiteSpace(image.github_repository))
            Console.WriteLine($"    https://github.com/{image.github_user.Trim()}/{image.github_repository.Trim()}");

        Console.WriteLine();
    }

    private static void FunFacts()
    {
        if (ServiceHelper.CoresService.InstalledCores.Count == 0)
        {
            return;
        }

        string[] sleepSupported = ServiceHelper.CoresService.InstalledCores
            .Where(c => ServiceHelper.CoresService.ReadCoreJson(c.id).framework.sleep_supported)
            .Select(c => c.id)
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
            Architecture arch = RuntimeInformation.ProcessArchitecture;

            return arch switch
            {
                Architecture.Arm64 => "win_arm64",
                _ => "win"
            };
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

        // ReadKey throws an InvalidOperationException when stdin is redirected
        // (headless / piped / no console). There is no key to wait for in that
        // case, so just return instead of crashing.
        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.ReadKey(true);
    }
}
