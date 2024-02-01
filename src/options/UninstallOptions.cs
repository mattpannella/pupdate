using CommandLine;

namespace Pannella.Options;

[Verb("uninstall",  HelpText = "Delete a core")]
public class UninstallOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option ('c', "core", Required = true, HelpText = "The core you want to delete.")]
    public string CoreName { get; set; }

    [Option('a', "assets", Required = false, HelpText = "Delete the core specific Assets folder")]
    public bool DeleteAssets { get; set; }
}
