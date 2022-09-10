namespace pannella.analoguepocket;

public class Settings
{
    public Firmware firmware { get; set; }
    public Config config { get; set; }
    public Dictionary<string, CoreSettings> coreSettings { get; set; }

    public Settings()
    {
        firmware = new Firmware();
    }
}