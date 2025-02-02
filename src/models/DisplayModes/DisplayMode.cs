// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global

namespace Pannella.Models.DisplayModes;

public class DisplayMode
{
    public string value { get; set; }
    public string description { get; set; }
    public int order { get; set; }

    public List<string> exclude_cores { get; set; } = new();

    public override string ToString()
    {
        return $"{this.value} - {this.order} - {this.description}";
    }
}
