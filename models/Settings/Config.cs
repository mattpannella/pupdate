namespace pannella.analoguepocket;

public class Config
{
    public bool download_assets { get; set; }
    public string archive_name { get; set; }
    public string? github_token { get; set; }
    public bool download_firmware { get; set; }
    public bool core_selector { get; set; }
    public bool preserve_images { get; set; }

    public Config()
    {
        download_assets = true;
        download_firmware = true;
        archive_name = "openFPGA-Files";
        core_selector = true;
        preserve_images = false;
    }
}