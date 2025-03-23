namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class Release
{
    public string downloadUrl { get; set; }
    public bool requiresLicense { get; set; }
    public AnalogueCore core { get; set; }
    public AnalogueData data { get; set; }
    public Updaters updaters { get; set; }
}
