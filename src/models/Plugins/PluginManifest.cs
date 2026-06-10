using Newtonsoft.Json;

namespace Pannella.Models.Plugins;

// Mirrors the plugin.json sidecar shape defined by pocket-plugin's demo_host.
public class PluginManifest
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("logo_url")]
    public string LogoUrl { get; set; }

    [JsonProperty("allowed_hosts")]
    public List<string> AllowedHosts { get; set; } = new();
}
