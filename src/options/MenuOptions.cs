using CommandLine;

namespace Pannella.Options;

[Verb("menu", isDefault: true, HelpText = "Interactive Main Menu")]
public class MenuOptions : BaseOptions
{
    // ReSharper disable once EmptyConstructor
    public MenuOptions() {}

    [Option('s', "skip-update", HelpText = "Skip the self update check", Required = false)]
    public bool SkipUpdate { get; set; }

    [Option('t', "tui", HelpText = "Launch the experimental full-screen TUI", Required = false)]
    public bool UseTui { get; set; }
}
