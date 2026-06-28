using CommandLine;

namespace Pannella.Options;

[Verb("menu", isDefault: true, HelpText = "Interactive Main Menu")]
public class MenuOptions : BaseOptions
{
    // ReSharper disable once EmptyConstructor
    public MenuOptions() {}

    [Option('s', "skip-update", HelpText = "Skip the self update check", Required = false)]
    public bool SkipUpdate { get; set; }

    [Option('t', "tui", HelpText = "Launch the new full-screen UI (Beta)", Required = false)]
    public bool UseTui { get; set; }

    [Option("classic", HelpText = "Force the classic menu and skip the startup UI prompt", Required = false)]
    public bool Classic { get; set; }
}
