using System.Linq.Expressions;
using System.Text;

namespace Pannella.Models;

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
        var links = new StringBuilder();
        var properties = typeof(Sponsor).GetProperties();

        foreach (var prop in properties)
        {
            object value = prop.GetValue(this, null);

            if (value == null) continue;

            links.AppendLine();

            if (value.GetType() == typeof(List<string>))
            {
                var stringArray = (List<string>)value;

                links.Append(string.Join(Environment.NewLine, stringArray));
            }
            else if (value is string)
            {
                links.Append(value);
            }
        }

        return links.ToString();
    }
}
