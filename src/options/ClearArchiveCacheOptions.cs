using CommandLine;

namespace Pannella.Options;

[Verb("clear-archive-cache", HelpText = "Delete cached archive downloads")]
public class ClearArchiveCacheOptions : BaseOptions
{
    // Confirmation reuses the global --yes/-y flag from BaseOptions.
}
