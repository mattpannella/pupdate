namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class Framework
{
    public string versionRequired { get; set; }
    public bool sleepSupported { get; set; }
    public Dock dock { get; set; }
    public Hardware hardware { get; set; }
}
