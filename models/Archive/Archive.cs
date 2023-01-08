namespace archiveorg;

using System.Linq;

public class Archive
{
    public int files_count { get; set; }
    public int item_last_updated { get; set; }
    public archiveorg.File[] files { get; set; }

    public archiveorg.File? GetFile(string filename)
    {
        archiveorg.File? file = files.Where(file => file.name == filename).FirstOrDefault() as archiveorg.File;
        return file;
    }
}