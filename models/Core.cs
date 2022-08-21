namespace pannella.analoguepocket;

public class Core
{
    public string name { get; set; }
    public bool allowPrerelease { get; set; }
    public Repo repo { get; set; }
    public Bios bios { get; set; }
    public bool skip { get; set; }
    public string platform { get; set; }
}