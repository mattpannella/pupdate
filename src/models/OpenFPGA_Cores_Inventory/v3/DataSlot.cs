namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class DataSlot
{
    public string Name { get; set; }
    public Parameters Parameters { get; set; }
    public string Filename { get; set; }
    public List<string> Extensions { get; set; }
}
