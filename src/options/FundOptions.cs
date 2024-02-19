using CommandLine;

namespace Pannella.Options;

[Verb("fund", HelpText = "List sponsor links")]
public class FundOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option('c', "core", HelpText = "The core to check funding links for", Required = false)]
    public string Core { get; set; }
}
