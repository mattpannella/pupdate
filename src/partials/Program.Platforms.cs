using Pannella.Helpers;
using Pannella.Options;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    private static void RunPlatforms(PlatformsOptions options)
    {
        CoresService coresService = ServiceHelper.CoresService;

        if (!string.IsNullOrEmpty(options.Archive))
        {
            foreach (string id in SplitIds(options.Archive))
            {
                coresService.ArchivePlatform(id);
            }
        }

        if (!string.IsNullOrEmpty(options.Unarchive))
        {
            foreach (string id in SplitIds(options.Unarchive))
            {
                coresService.UnarchivePlatform(id);
            }
        }

        if (options.ArchiveUnused)
        {
            int archived = coresService.ArchiveUnusedPlatforms();

            Console.WriteLine($"Archived {archived} unused platform(s).");
        }

        // default to listing if no action was requested, or when explicitly asked
        bool noAction = string.IsNullOrEmpty(options.Archive) &&
                        string.IsNullOrEmpty(options.Unarchive) &&
                        !options.ArchiveUnused;

        if (options.List || noAction)
        {
            var platforms = coresService.GetPlatforms();
            int activeCount = platforms.Count(p => !p.Archived);

            Console.WriteLine($"Active platforms: {activeCount} / {CoresService.PLATFORM_LIMIT}");
            Console.WriteLine();

            foreach (var platform in platforms)
            {
                string status = platform.Archived
                    ? "archived"
                    : platform.HasInstalledCore ? "active" : "active, unused";

                Console.WriteLine($"  {platform.Id,-28} {platform.Name,-32} [{status}]");
            }
        }
    }

    private static IEnumerable<string> SplitIds(string value)
    {
        return value
            .Split(',')
            .Select(id => id.Trim())
            .Where(id => !string.IsNullOrEmpty(id));
    }
}
