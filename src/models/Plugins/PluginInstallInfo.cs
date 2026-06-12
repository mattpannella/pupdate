using Newtonsoft.Json;

namespace Pannella.Models.Plugins;

// Sidecar file (`installed.json`) stored next to plugin.wasm / plugin.json.
// Tracks where the plugin came from + which release tag is installed so we
// can check for updates. Local-only — not part of pocket-plugin's upstream
// plugin.json schema.
public class PluginInstallInfo
{
    [JsonProperty("repo")]
    public string Repo { get; set; } // "owner/repo"

    [JsonProperty("release_tag")]
    public string ReleaseTag { get; set; }

    [JsonProperty("installed_at")]
    public string InstalledAt { get; set; }
}
