// ReSharper disable InconsistentNaming

using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class Settings
{
    public Config config { get; set; } = new();
    public SortedDictionary<string, CoreSettings> core_settings { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);
    public Debug debug { get; set; } = new();
    public Credentials credentials { get; set; }

    [JsonProperty]
    private SortedDictionary<string, CoreSettings> coreSettings { set => this.core_settings = value; }
}
