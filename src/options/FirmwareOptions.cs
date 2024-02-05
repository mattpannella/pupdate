using CommandLine;

namespace Pannella.Options;

[Verb("firmware",  HelpText = "Check for Pocket firmware updates")]
public class FirmwareOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }
}
