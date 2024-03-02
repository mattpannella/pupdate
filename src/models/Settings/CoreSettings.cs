using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class CoreSettings
{
    public bool skip { get; set; }
    public bool download_assets { get; set; } = true;
    public bool platform_rename { get; set; } = true;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool pocket_extras { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string pocket_extras_version { get; set; } = null;
}
