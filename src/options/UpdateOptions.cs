using CommandLine;

namespace Pannella.Options;

[Verb("update",  HelpText = "Run update all. (You can configure via the settings menu)")]
public class UpdateOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option ('c', "core", Required = false, HelpText = "The core you want to update.")]
    public string CoreName { get; set; }

    [Option('f', "platformsfolder", Required = false, HelpText = "Preserve the Platforms folder, so customizations aren't overwritten by updates.")]
    public bool PreservePlatformsFolder { get; set; }

    [Option('r', "clean", Required = false, HelpText = "Clean install. Remove all existing core files, before updating")]
    public bool CleanInstall { get; set; }

}
