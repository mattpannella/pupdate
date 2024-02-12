using CommandLine;

namespace Pannella.Options;

[Verb("gameboy-palettes", HelpText = "Downloads and installs the GameBoy Palettes")]
public class GameBoyPalettesOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }
}
