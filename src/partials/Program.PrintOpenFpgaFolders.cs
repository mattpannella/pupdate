using Pannella.Helpers;

namespace Pannella;

internal partial class Program
{
    private static void PrintOpenFpgaCategories()
    {
        var openFpgaFolders = new SortedDictionary<string, List<string>>();

        foreach (var core in ServiceHelper.CoresService.InstalledCores)
        {
            var platform = ServiceHelper.CoresService.ReadPlatformJson(core.identifier);
            var item = platform.name;

            if (!openFpgaFolders.TryAdd(platform.category, new List<string> { item }))
            {
                if (!openFpgaFolders[platform.category].Contains(item))
                {
                    openFpgaFolders[platform.category].Add(item);
                }
            }
        }

        Console.WriteLine("Open FPGA Categories:");

        foreach (var kvp in openFpgaFolders)
        {
            Console.WriteLine($"  {kvp.Key}");

            foreach (var item in kvp.Value.Order())
            {
                Console.WriteLine($"    {item}");
            }

            Console.WriteLine();
        }
    }
}
