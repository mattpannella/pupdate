using CommandLine;

namespace Pannella.Options;

[Verb("pocket-extras", HelpText = "Download Pocket Extras")]
public class PocketExtrasOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location")]
    public string InstallPath { get; set; }

    [Option('n', "name", HelpText = "The name of the extra you want to install")]
    public string Name { get; set; }

    [Option('l', "list", HelpText = "List out the values allowed for name")]
    public bool List { get; set; }

    [Option('i', "info", HelpText = "Shows the details for the specified name")]
    public bool Info { get; set; }
}
