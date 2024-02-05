namespace Pannella.Models.Github;

public class Release
{
    public string tag_name { get; set; }
    public string name { get; set; }
    public bool prerelease { get; set; }
    public string url { get; set; }
    public List<Asset> assets { get; set; }
    public bool draft { get; set; }
    public string html_url { get; set; }
}
