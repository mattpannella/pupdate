namespace Pannella.Models.Analogue.Instance.Simple;

public class SimpleInstance
{
    public SimpleDataSlot[] data_slots { get; set; }
    public string data_path { get; set; } = string.Empty;
    public string magic { get; set; } = "APF_VER_1";
}
