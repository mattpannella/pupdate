// ReSharper disable InconsistentNaming

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class Core
{
    public string id { get; set; }
    public Repository repository { get; set; }
    public List<Release> releases { get; set; }
}
