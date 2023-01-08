namespace archiveorg;

public class JSON
{
    public int files_count { get; set; }
    public int last_item_updated { get; set; }
    public archiveorg.File[] files { get; set; }
}