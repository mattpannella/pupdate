using CommandLine;

namespace Pannella.Options;

[Verb("pocket-extras", HelpText = "Download Pocket Extras")]
public class PocketExtrasOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option ('n', "name", Required = true, HelpText = "The name of the extra you want to install")]
    public string Name { get; set; }
}
