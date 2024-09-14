namespace Pannella.Models.OpenFPGA_Cores_Inventory;

public class Core
{
    public string identifier { get; set; }
    public Repository repository { get; set; }
    public Platform platform { get; set; }
    public string platform_id { get; set; }
    public Sponsor sponsor { get; set; }
    public string download_url { get; set; }
    public string release_date { get; set; }
    public string version { get; set; }
    public string license_slot_id;
    public int license_slot_platform_id_index;
    public string license_slot_filename;
    public bool requires_license { get; set; } = false;

    public override string ToString()
    {
        return $"{platform.name} ({identifier})";
    }
}
