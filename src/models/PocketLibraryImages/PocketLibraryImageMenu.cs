// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global

namespace Pannella.Models.PocketLibraryImages;

public class PocketLibraryImageMenu
{
    public string id { get; set; }

    public string menu_title { get; set; }

    public List<PocketLibraryImage> entries { get; set; }
}
