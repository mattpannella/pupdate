namespace Pannella.Models.InstancePackager;

public class DataSlot
{
    public int id { get; set; }
    public string filename { get; set; }
    public bool required { get; set; }
    public string sort { get; set; } = "ascending";
    public bool as_filename { get; set; }
}
