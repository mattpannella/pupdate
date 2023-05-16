namespace pannella.analoguepocket;

public class CoreSettings
{
    public bool skip { get; set; }
    public bool download_assets { get; set; }
    public bool platform_rename { get; set; }

    public CoreSettings()
    {
        skip = false;
        download_assets = true;
        platform_rename = true;
    }
}