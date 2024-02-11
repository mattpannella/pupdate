using System.Text.Json.Serialization;

namespace Pannella.Models.Settings;

public class CoreSettings
{
    public bool skip { get; set; }
    public bool download_assets { get; set; } = true;
    public bool platform_rename { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool pocket_extras { get; set; }
}
