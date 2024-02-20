using CommandLine;

namespace Pannella.Options;

[Verb("backup-saves", HelpText = "Create a compressed zip file of the Saves & Memories directories.")]
public class BackupSavesOptions : BaseOptions
{
    [Option('l', "location", HelpText = "Absolute path to backup location", Required = true)]
    public string BackupPath { get; set; } = null!;

    [Option('s', "save", HelpText = "Save settings to the config file", Required = false)]
    public bool Save { get; set; }
}
