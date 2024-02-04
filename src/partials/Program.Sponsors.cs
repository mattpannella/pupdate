using System.Text;
using Pannella.Helpers;
using Pannella.Models;

namespace Pannella;

internal partial class Program
{
    private static string SponsorLinksHelper(Sponsor sponsor)
    {
        var links = new StringBuilder();

        var properties = typeof(Sponsor).GetProperties();

        foreach (var prop in properties)
        {
            object value = prop.GetValue(sponsor, null);
            if (value != null && value.GetType() == typeof(List<string>))
            {
                //String.Join didn't want to work unless I explicitely cast this
                var stringArray = value as List<string>;
                links.AppendLine();
                links.AppendLine();
                links.Append(string.Join(Environment.NewLine, stringArray));
            }
            else if (value != null && value.GetType() == typeof(string))
            {
                links.AppendLine();
                links.AppendLine();
                links.Append(value);
            }
        }

        return links.ToString();
    }

    private static string GetRandomSponsorLinks()
    {
        var output = new StringBuilder();

        if (GlobalHelper.InstalledCores.Count > 0)
        {
            var random = new Random();
            var index = random.Next(GlobalHelper.InstalledCores.Count);
            var randomItem = GlobalHelper.InstalledCores[index];

            if (randomItem.sponsor != null)
            {
                var links = SponsorLinksHelper(randomItem.sponsor);
                var author = randomItem.GetConfig().metadata.author;

                output.AppendLine();
                output.AppendLine($"Please consider supporting {author} for their work on the {randomItem} core:");
                output.Append(links.Trim());
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
                var links = SponsorLinksHelper(core.sponsor);

                Console.WriteLine();
                Console.WriteLine($"{core.identifier}:");
                Console.WriteLine(links.Trim());
            }
        }
    }
}
