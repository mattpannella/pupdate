using Newtonsoft.Json;
// ReSharper disable InconsistentNaming
// ReSharper disable RedundantDefaultMemberInitializer

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

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool display_modes { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string original_display_modes { get; set; } = null;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string selected_display_modes { get; set; } = null;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool requires_license { get; set; } = false;
}
