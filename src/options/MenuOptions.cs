using CommandLine;

namespace Pannella.Options;

[Verb("menu", isDefault: true, HelpText = "Interactive Main Menu")]
public class MenuOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option('s', "skip-update", HelpText = "Skip the self update check", Required = false)]
    public bool SkipUpdate { get; set; }
}
