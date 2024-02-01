using CommandLine;

namespace Pannella.Options;

[Verb("assets",  HelpText = "Run the asset downloader")]
public class AssetsOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option ('c', "core", Required = false, HelpText = "The core you want to download assets for.")]
    public string CoreName { get; set; }
}
