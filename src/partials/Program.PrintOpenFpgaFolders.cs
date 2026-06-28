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
            var item = platform.name;

            if (!openFpgaFolders.TryAdd(platform.category, new List<string> { item }))
            {
                if (!openFpgaFolders[platform.category].Contains(item))
                {
                    openFpgaFolders[platform.category].Add(item);
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
