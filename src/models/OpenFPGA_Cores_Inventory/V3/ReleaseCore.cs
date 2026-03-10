// ReSharper disable InconsistentNaming

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class ReleaseCore
{
    public ReleaseMetadata metadata { get; set; }
    public ReleaseFramework framework { get; set; }
    public ReleaseDock dock { get; set; }
    public ReleaseHardware hardware { get; set; }
}
