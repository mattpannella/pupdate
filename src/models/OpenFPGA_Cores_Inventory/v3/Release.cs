namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class Release
{
    public string DownloadUrl { get; set; }
    public bool RequiresLicense { get; set; }
    public AnalogueCore Core { get; set; }
    public AnalogueData Data { get; set; }
    public Updaters Updaters { get; set; }
}
