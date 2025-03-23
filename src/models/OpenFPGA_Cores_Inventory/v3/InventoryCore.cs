namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class InventoryCore
{
    public string id { get; set; }
    public Repository repository { get; set; }
    public List<Release> releases { get; set; }
}
