// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global

namespace Pannella.Models.PocketLibraryImages;

public class PocketLibraryImage
{
    public string id { get; set; }

    public string menu_label { get; set; }

    public string description { get; set; }

    public string github_user { get; set; }

    public string github_repository { get; set; }

    public List<PocketLibraryImageSource> sources { get; set; }

    public string post_install_note { get; set; }

    public override string ToString()
    {
        return this.id;
    }
}
