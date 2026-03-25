// ReSharper disable InconsistentNaming

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class ReleaseMetadata
{
    public List<string> platform_ids { get; set; }
    public string version { get; set; }
    public string date_release { get; set; }
}
