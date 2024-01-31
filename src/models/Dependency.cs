namespace pannella.analoguepocket;

public class Dependency
{
    public string location { get; set; }
    public List<DependencyFile> files { get; set; }

    public Dependency()
    {
        files = new List<DependencyFile>();
    }
}

public class DependencyFile
{
    public string url { get; set; }
    public string file_name { get; set; }
    public bool zip { get; set; }
    public string zip_file { get; set; }
    public string overrideLocation { get; set; }
    public string archive_file { get; set; }
    public string archive_zip { get; set; }
}