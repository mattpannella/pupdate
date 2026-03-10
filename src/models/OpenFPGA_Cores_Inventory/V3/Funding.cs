// ReSharper disable InconsistentNaming

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

/// <summary>Funding/sponsor links in v3 API (maps to Sponsor for display).</summary>
public class Funding
{
    public List<string> github { get; set; }
    public string patreon { get; set; }
    public List<string> custom { get; set; }
}
