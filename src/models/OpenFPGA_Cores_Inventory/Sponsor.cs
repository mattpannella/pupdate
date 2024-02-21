using System.Text;

namespace Pannella.Models.OpenFPGA_Cores_Inventory;

public class Sponsor
{
    public string community_bridge { get; set; }
    public List<string> github { get; set; }
    public string issuehunt { get; set; }
    public string ko_fi { get; set; }
    public string liberapay { get; set; }
    public string open_collective { get; set; }
    public string otechie { get; set; }
    public string patreon { get; set; }
    public string tidelift { get; set; }
    public List<string> custom { get; set; }

    public override string ToString()
    {
        return this.ToString(string.Empty);
    }

    public string ToString(string padding)
    {
        var links = new StringBuilder();
        var properties = typeof(Sponsor).GetProperties();

        foreach (var prop in properties)
        {
            object value = prop.GetValue(this, null);

            if (value == null)
                continue;

            if (value.GetType() == typeof(List<string>))
            {
                var stringArray = (List<string>)value;

                foreach (var item in stringArray)
                {
                    links.Append(padding);
                    links.AppendLine(item);
                }
            }
            else if (value is string)
            {
                links.Append(padding);
                links.Append(value);
                links.AppendLine();
            }
        }

        return links.ToString();
    }
}
