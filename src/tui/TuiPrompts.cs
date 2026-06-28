using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Small TUI prompt helpers (modal MessageBox-backed). The TUI side of what will become the
/// shared IUserIo abstraction in the cleanup phase; for now the classic menu keeps its own
/// Console-based prompts.
/// </summary>
internal static class TuiPrompts
{
    /// <summary>Yes/No confirmation. Returns true only if the user picks Yes.</summary>
    public static bool Confirm(IApplication app, string title, string message)
        => MessageBox.Query(app, title, message, "Yes", "No") == 0;
}
