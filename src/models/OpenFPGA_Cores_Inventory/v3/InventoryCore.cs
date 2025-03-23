namespace Pannella.Models.OpenFPGA_Cores_Inventory.v3;

public class InventoryCore
{
    public string Id { get; set; }
    public Repository Repository { get; set; }
    public List<Release> Releases { get; set; }
}
