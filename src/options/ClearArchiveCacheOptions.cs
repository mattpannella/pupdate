using CommandLine;

namespace Pannella.Options;

[Verb("clear-archive-cache", HelpText = "Delete cached archive downloads")]
public class ClearArchiveCacheOptions : BaseOptions
{
    [Option('y', "yes", HelpText = "Confirm clearing the archive cache (required for non-interactive use)")]
    public bool Yes { get; set; }
}
