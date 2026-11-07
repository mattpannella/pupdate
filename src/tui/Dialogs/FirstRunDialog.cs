using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;

namespace Pannella.TUI;

/// <summary>
/// Fresh install: asks which cores to start with (the TUI version of the classic first-run menu).
/// Closing the dialog starts with none, so Update All never installs every core by accident.
/// </summary>
internal static class FirstRunDialog
{
    public static void ShowIfNeeded()
    {
        if (!Program.FirstRunCoreSetupNeeded())
        {
            return;
        }

        var options = Program.FirstRunOptions();
        int? index = SelectDialog.Show("Welcome to pupdate", Program.FirstRunQuestion(),
            options.Select(o => o.Label).ToList());
        var choice = index == null ? FirstRunChoice.None : options[index.Value].Choice;
        HashSet<string> types = null;

        if (choice == FirstRunChoice.ByType)
        {
            var available = Program.FirstRunTypes();
            var picked = ChecklistDialog.Show("Choose by type", "Check the kinds of cores you want:",
                available.Select(t => $"{t.Type} ({t.Count} cores)").ToList(), _ => false, "Continue");

            types = picked == null
                ? new HashSet<string>()
                : picked.Select(i => available[i].Type).ToHashSet();
        }

        TuiApp.PostStatus(Program.ApplyFirstRunChoice(choice, types));
    }
}
