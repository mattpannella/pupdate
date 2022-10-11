namespace pannella.analoguepocket;

public class CoreSettings
{
    public bool skip { get; set; }
    public bool allowPrerelease { get; set; }

    public CoreSettings()
    {
        skip = false;
        allowPrerelease = false;
    }
}