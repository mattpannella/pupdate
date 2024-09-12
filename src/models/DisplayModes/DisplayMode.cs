namespace Pannella.Models.DisplayModes;

public class DisplayMode
{
    public string value { get; set; }
    public string description { get; set; }

    public override string ToString()
    {
        return value;
    }
}
