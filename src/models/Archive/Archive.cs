namespace Pannella.Models.Archive;

public class Archive
{
    public int files_count { get; set; }
    public int item_last_updated { get; set; }
    public List<File> files { get; set; }
}
