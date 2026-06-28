using System.Text;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella;

internal static partial class Program
{
    // A randomly-chosen welcome banner (the ASCII art the classic menu prints at the top), with
    // blank leading/trailing lines trimmed so it can be pinned as a fixed-height TUI header.
    internal static string RandomWelcomeBanner()
    {
        var lines = WELCOME_MESSAGES[new Random().Next(WELCOME_MESSAGES.Length)]
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return string.Join("\n", lines);
    }

    internal static string GetRandomSponsorLinks()
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

            var funding = randomCore.repository?.funding?.ToString();

            if (!string.IsNullOrWhiteSpace(funding))
            {
                output.AppendLine($"Please consider supporting {author} for their work on the {randomCore} core:");
                output.Append(funding);
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

        foreach (var core in cores.Where(core => core.repository?.funding != null))
        {
            Console.WriteLine($"{core.id}:");
            Console.WriteLine(core.repository.funding.ToString("    "));
        }
    }
}
