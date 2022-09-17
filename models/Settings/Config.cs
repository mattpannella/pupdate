namespace pannella.analoguepocket;

public class Config
{
    public bool download_bios { get; set; }
    public bool download_firmware { get; set; }

    public Config()
    {
        download_bios = true;
        download_firmware = true;
    }
}