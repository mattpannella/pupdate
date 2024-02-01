namespace Pannella.Models.Archive;

public class Archive
{
    public int files_count { get; set; }
    public int item_last_updated { get; set; }
    public File[] files { get; set; }

    public File GetFile(string filename)
    {
        File file = this.files.FirstOrDefault(file => file.name == filename);

        return file;
    }
}
