using CommandLine;

namespace Pannella.Options;

[Verb("analogizer-setup",  HelpText = "Set up Analogizer options")]
public class AnalogizerSetupOptions : BaseOptions
{
    [Option ('j', "jotego", Required = false, HelpText = "Run the setup for Jotego's cores.")]
    public bool Jotego { get; set; } = false;
}
