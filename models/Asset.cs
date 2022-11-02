namespace pannella.analoguepocket;

public class Asset
{
    public string platform{ get; set; } = "";
    public string? filename { get; set; }
    public bool core_specific { get; set; }
    public List<string>? extensions { get; set; }
}