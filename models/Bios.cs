namespace pannella.analoguepocket;

public class Bios
{
    public string location { get; set; }
    public List<BiosFile> files { get; set; }
}

public class BiosFile
{
    public string url { get; set; }
    public string file_name { get; set; }
    public bool zip { get; set; }
    public string zip_file { get; set; }
}