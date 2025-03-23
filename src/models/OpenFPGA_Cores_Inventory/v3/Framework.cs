namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class Framework
{
    public string VersionRequired { get; set; }
    public bool SleepSupported { get; set; }
    public Dock Dock { get; set; }
    public Hardware Hardware { get; set; }
}
