using System.Text;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella;

internal partial class Program
{
    private static string GetRandomSponsorLinks()
    {
        var output = new StringBuilder();

        if (ServiceHelper.CoresService.InstalledCoresWithSponsors.Count > 0)
        {
            var random = new Random();
            var index = random.Next(ServiceHelper.CoresService.InstalledCoresWithSponsors.Count);
            var randomItem = ServiceHelper.CoresService.InstalledCoresWithSponsors[index];

            if (randomItem.sponsor != null)
            {
                var info = ServiceHelper.CoresService.ReadCoreJson(randomItem.identifier);
                var author = info.metadata.author;

                output.AppendLine($"Please consider supporting {author} for their work on the {randomItem} core:");
                output.Append(randomItem.sponsor);
            }
        }

        return output.ToString();
    }

    private static void Funding(string identifier)
    {
        if (ServiceHelper.CoresService.InstalledCores.Count == 0)
        {
            Console.WriteLine("You must install cores to see their funding information.");
            return;
        }

        List<Core> cores;

        if (string.IsNullOrEmpty(identifier))
        {
            cores = ServiceHelper.CoresService.InstalledCores;
        }
        else
        {
            cores = new List<Core>();

            var core = ServiceHelper.CoresService.GetInstalledCore(identifier);

            if (core != null)
            {
                cores.Add(core);
            }
        }

        Console.WriteLine();

        foreach (var core in cores.Where(core => core.sponsor != null))
        {
            Console.WriteLine($"{core.identifier}:");
            Console.WriteLine(core.sponsor.ToString("    "));
        }
    }
}
