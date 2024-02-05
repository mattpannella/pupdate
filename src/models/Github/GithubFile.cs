namespace Pannella.Models.Github;

public class File
{
    public string name {get; set;}
    public string path {get; set;}
    public string sha {get; set;}
    public int? size {get; set;}
    public string url {get; set;}
    public string html_url {get; set;}
    public string download_url {get; set;}
}
