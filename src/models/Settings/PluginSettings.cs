// ReSharper disable InconsistentNaming

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pannella.Models.Settings;

public enum PluginPhase
{
    after,
    before
}

public class PluginSettings
{
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string repo { get; set; }

    public bool run_on_update { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public PluginPhase phase { get; set; } = PluginPhase.after;
}
