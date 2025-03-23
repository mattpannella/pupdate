namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class DataSlot
{
    public string name { get; set; }
    public Parameters parameters { get; set; }
    public string filename { get; set; }
    public List<string> extensions { get; set; }
}
