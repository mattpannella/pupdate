using CommandLine;

namespace Pannella.Options;

[Verb("platforms", HelpText = "Archive or unarchive Pocket platform files")]
public class PlatformsOptions : BaseOptions
{
    [Option('l', "list", Required = false, HelpText = "List platforms and their archive status")]
    public bool List { get; set; }

    [Option('a', "archive", Required = false, HelpText = "Comma-separated platform ids to archive")]
    public string Archive { get; set; }

    [Option('u', "unarchive", Required = false, HelpText = "Comma-separated platform ids to unarchive")]
    public string Unarchive { get; set; }

    [Option("archive-unused", Required = false, HelpText = "Archive all platforms with no installed core")]
    public bool ArchiveUnused { get; set; }
}
