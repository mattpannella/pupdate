// ReSharper disable InconsistentNaming

using System.Text;

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class Funding
{
    public List<string> github { get; set; }
    public string patreon { get; set; }
    public string ko_fi { get; set; }
    public List<string> custom { get; set; }

    public List<string> GetLinks()
    {
        var links = new List<string>();

        if (github != null)
        {
            links.AddRange(github.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        if (!string.IsNullOrWhiteSpace(patreon))
        {
            links.Add(patreon);
        }

        if (!string.IsNullOrWhiteSpace(ko_fi))
        {
            links.Add(ko_fi);
        }

        if (custom != null)
        {
            links.AddRange(custom.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        return links;
    }

    public override string ToString()
    {
        return ToString(string.Empty);
    }

    public string ToString(string padding)
    {
        var links = new StringBuilder();

        if (github != null)
        {
            foreach (var item in github)
            {
                links.Append(padding);
                links.AppendLine(item);
            }
        }

        if (!string.IsNullOrEmpty(patreon))
        {
            links.Append(padding);
            links.AppendLine(patreon);
        }

        if (!string.IsNullOrEmpty(ko_fi))
        {
            links.Append(padding);
            links.AppendLine(ko_fi);
        }

        if (custom != null)
        {
            foreach (var item in custom)
            {
                links.Append(padding);
                links.AppendLine(item);
            }
        }

        return links.ToString();
    }
}
