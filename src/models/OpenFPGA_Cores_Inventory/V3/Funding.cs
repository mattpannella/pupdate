// ReSharper disable InconsistentNaming

using System.Text;

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class Funding
{
    public List<string> github { get; set; }
    public string patreon { get; set; }
    public List<string> custom { get; set; }

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
