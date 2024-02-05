namespace Pannella.Models.Settings;

public class Settings
{
    public Firmware firmware { get; set; } = new();
    public Config config { get; set; } = new();
    public Dictionary<string, CoreSettings> coreSettings { get; set; } = new();
}
