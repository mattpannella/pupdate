using Pannella.Helpers;

namespace Pannella;

internal static partial class Program
{
    internal static void PrintOpenFpgaCategories(Action<string> log = null)
    {
        log ??= Console.WriteLine;

        var openFpgaFolders = new SortedDictionary<string, List<string>>();

        foreach (var core in ServiceHelper.CoresService.InstalledCores)
        {
            var platform = ServiceHelper.CoresService.ReadPlatformJson(core.id);

            // ReadPlatformJson returns null when a core's core.json or platform file is missing;
            // fall back for a missing name/category so one bad core can't abort the whole listing.
            if (platform == null)
            {
                continue;
            }

            var item = platform.name ?? core.id;
            var category = platform.category ?? "(uncategorized)";

            if (!openFpgaFolders.TryAdd(category, new List<string> { item }))
            {
                if (!openFpgaFolders[category].Contains(item))
                {
                    openFpgaFolders[category].Add(item);
                }
            }
        }

        log("Open FPGA Categories:");

        foreach (var kvp in openFpgaFolders)
        {
            log($"  {kvp.Key}");

            foreach (var item in kvp.Value.Order())
            {
                log($"    {item}");
            }

            log(string.Empty);
        }
    }
}
