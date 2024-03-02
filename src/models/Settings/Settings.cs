using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class Settings
{
    public Config config { get; set; } = new();
    public Dictionary<string, CoreSettings> core_settings { get; set; } = new();

    [JsonProperty]
    private Dictionary<string, CoreSettings> coreSettings { set => this.core_settings = value; }
}
