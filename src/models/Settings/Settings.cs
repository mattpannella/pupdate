namespace Pannella.Models.Settings;

public class Settings
{
    public Config config { get; set; } = new();
    public Dictionary<string, CoreSettings> coreSettings { get; set; } = new();
}
