using System.Text;
using Pannella.Helpers;
using Pannella.Models;

namespace Pannella;

internal partial class Program
{
    private static string GetRandomSponsorLinks()
    {
        var output = new StringBuilder();

        if (GlobalHelper.InstalledCoresWithSponsors.Count > 0)
        {
            var random = new Random();
            var index = random.Next(GlobalHelper.InstalledCoresWithSponsors.Count);
            var randomItem = GlobalHelper.InstalledCoresWithSponsors[index];

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
        if (GlobalHelper.InstalledCores.Count == 0)
        {
            return;
        }

        List<Core> cores = new List<Core>();

        if (identifier == null)
        {
            cores = GlobalHelper.InstalledCores;
        }
        else
        {
            var c = GlobalHelper.GetCore(identifier);

            if (c != null && c.IsInstalled())
            {
                cores.Add(c);
            }
        }

        foreach (Core core in cores)
        {
            if (core.sponsor != null)
            {
                Console.WriteLine();
                Console.WriteLine($"{core.identifier}:");
                Console.WriteLine(core.sponsor);
            }
        }
    }
}
