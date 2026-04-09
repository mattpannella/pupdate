using CommandLine;

namespace Pannella.Options;

[Verb("pocket-library-images", HelpText = "Download Pocket library images (Spiritualized archive by default; use -n for catalog entries from pocket_library_images.json)")]
public class PocketLibraryImagesOptions : BaseOptions
{
    [Option('n', "name", HelpText = "Catalog entry id to install (see -l). Omit for Spiritualized archive package.")]
    public string Name { get; set; }

    [Option('l', "list", HelpText = "List catalog entry ids and descriptions")]
    public bool List { get; set; }

    [Option('i', "info", HelpText = "Show details for the entry given by -n/--name")]
    public bool Info { get; set; }
}
