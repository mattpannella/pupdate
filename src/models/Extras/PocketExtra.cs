using System.Text.Json.Serialization;

namespace Pannella.Models.Extras;

public class PocketExtra
{
    public string id { get; set; }

    public string name { get; set; }

    public string description { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PocketExtraType type { get; set; }

    public List<string> core_identifiers { get; set; }

    public bool has_placeholders { get; set; }

    public string github_user { get; set; }

    public string github_repository { get; set; }

    public string github_asset_prefix { get; set; }

    public List<string> additional_links { get; set; } = new();

    public override string ToString()
    {
        return this.id;
    }
}
