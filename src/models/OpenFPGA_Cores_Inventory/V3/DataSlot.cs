// ReSharper disable InconsistentNaming

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class DataSlot
{
    public string name { get; set; }
    public DataSlotParameters parameters { get; set; }
    public string filename { get; set; }
    public List<string> extensions { get; set; }
}

public class DataSlotParameters
{
    public bool core_specific_file { get; set; }
    public bool instance_json { get; set; }
    public int platform_index { get; set; }
}
