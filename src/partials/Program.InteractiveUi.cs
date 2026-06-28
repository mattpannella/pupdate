using Pannella.Helpers;
using Pannella.Options;

namespace Pannella;

internal static partial class Program
{
    // Decides which interactive UI to launch: explicit flag/env wins, then the saved preference,
    // otherwise a one-time startup prompt whose answer is remembered. Verbs and unattended (--yes)
    // runs never enter the TUI here (they keep the classic console sinks).
    private static bool ResolveInteractiveUi(object parsed)
    {
        if (parsed is not MenuOptions options)
        {
            return false;
        }

        if (options.UseTui ||
            string.Equals(Environment.GetEnvironmentVariable("PUPDATE_TUI"), "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (options.Classic)
        {
            return false;
        }

        var config = ServiceHelper.SettingsService.Config;

        // Ask once (unless unattended) and remember it. Re-changeable later via the "use_tui"
        // setting, which is surfaced as a toggle in the classic settings menu and the TUI Settings tab.
        if (!config.ui_prompt_completed && !AssumeYes)
        {
            config.use_tui = PromptForUi();
            config.ui_prompt_completed = true;
            ServiceHelper.SettingsService.Save();
        }

        return config.use_tui;
    }

    private static bool PromptForUi()
    {
        Console.WriteLine();
        Console.WriteLine("Which interface would you like to use?");
        Console.WriteLine("  [1] Classic menu");
        Console.WriteLine("  [2] New UI (Beta)");
        Console.Write("Choose [1/2] (default 1): ");

        ConsoleKey key = Console.ReadKey(intercept: false).Key;

        Console.WriteLine();

        return key is ConsoleKey.D2 or ConsoleKey.NumPad2;
    }
}
