using CommandLine;

namespace Pannella.Options;

[Verb("menu-cache", HelpText = "Analyze or regenerate the Pocket openFPGA menu cache (experimental)")]
public class MenuCacheOptions : BaseOptions
{
    [Option("verify", Required = false,
        HelpText = "Regenerate the cache files and byte-compare them against the ones in /System.")]
    public bool Verify { get; set; }

    [Option('o', "output", Required = false,
        HelpText = "Directory to write the generated cache file(s) to. Never writes into /System directly.")]
    public string Output { get; set; }

    [Option("write-system", Required = false,
        HelpText = "Write the generated cache set into /System (backing up existing files) so the Pocket loads " +
                   "it without rebuilding. Refuses to write when the menu would exceed the Pocket's 32 KB limit.")]
    public bool WriteSystem { get; set; }
}
