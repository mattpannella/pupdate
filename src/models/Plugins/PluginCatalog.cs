// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pannella.Models.Settings;
using Pannella.Services;

namespace Pannella.Models.Plugins;

public class PluginCatalog
{
    public List<PluginCatalogEntry> plugins { get; set; } = new();
}

public class PluginCatalogEntry
{
    public string id { get; set; }

    public string repo { get; set; }

    public string name { get; set; }

    public string author { get; set; }

    public string description { get; set; }

    public List<string> tags { get; set; } = new();

    // True when the plugin asks questions, so an unattended Update All (-y) would skip it.
    public bool requires_input { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public PluginPhase? phase { get; set; }

    public List<string> links { get; set; } = new();

    [JsonIgnore]
    public string Slug => PluginService.NormalizeRepoSlug(this.repo);

    [JsonIgnore]
    public string DirectoryName => PluginService.DirectoryNameFor(this.Slug);

    [JsonIgnore]
    public string RepoUrl => this.Slug == null ? this.repo : $"https://github.com/{this.Slug}";

    public override string ToString()
    {
        return this.id;
    }
}
