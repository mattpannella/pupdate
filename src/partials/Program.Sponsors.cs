using System.Text;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.v3;

namespace Pannella;

internal static partial class Program
{
    private static string GetRandomSponsorLinks()
    {
        var output = new StringBuilder();

        if (ServiceHelper.CoresService.InstalledCoresWithSponsors.Count > 0)
        {
            var random = new Random(DateTime.Now.Millisecond);
            var keyIndex = random.Next(ServiceHelper.CoresService.InstalledCoresWithSponsors.Count);
            var author = ServiceHelper.CoresService.InstalledCoresWithSponsors.Keys.ElementAt(keyIndex);
            var authorCores = ServiceHelper.CoresService.InstalledCoresWithSponsors[author];
            var coreIndex = random.Next(authorCores.Count);
            var randomCore = authorCores[coreIndex];

            output.AppendLine($"Please consider supporting {author} for their work on the {randomCore} core:");
            output.Append(randomCore.sponsor);
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
