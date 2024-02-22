using System.Text;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static string GetRandomSponsorLinks()
    {
        var output = new StringBuilder();

        if (CoresService.InstalledCoresWithSponsors.Count > 0)
        {
            var random = new Random();
            var index = random.Next(CoresService.InstalledCoresWithSponsors.Count);
            var randomItem = CoresService.InstalledCoresWithSponsors[index];

            if (randomItem.sponsor != null)
            {
                var author = randomItem.GetConfig().metadata.author;

                output.AppendLine($"Please consider supporting {author} for their work on the {randomItem} core:");
                output.Append(randomItem.sponsor);
            }
        }

        return output.ToString();
    }

    private static void Funding(string identifier)
    {
        if (CoresService.InstalledCores.Count == 0)
        {
            Console.WriteLine("You must install cores to see their funding information.");
            return;
        }

        List<Core> cores;

        if (string.IsNullOrEmpty(identifier))
        {
            cores = CoresService.InstalledCores;
        }
        else
        {
            cores = new List<Core>();

            var core = CoresService.GetInstalledCore(identifier);

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
