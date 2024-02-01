using CommandLine;

namespace Pannella.Options;

[Verb("backup-saves", HelpText = "Create a compressed zip file of the Saves directory.")]
public class BackupSavesOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option('l', "location", HelpText = "Absolute path to backup location", Required = true)]
    public string BackupPath { get; set; } = null!;

    [Option('s', "save", HelpText = "Save settings to the config file", Required = false)]
    public bool Save { get; set; }
}
