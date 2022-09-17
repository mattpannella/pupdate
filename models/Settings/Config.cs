namespace pannella.analoguepocket;

public class Config
{
    public bool download_assets { get; set; }
    public string archive_name { get; set; }

    public Config()
    {
        download_assets = true;
        archive_name = "pocket_roms";
    }
}