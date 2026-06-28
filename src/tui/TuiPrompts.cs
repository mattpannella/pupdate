using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Small TUI prompt helpers (modal). The TUI side of what will become the shared IUserIo
/// abstraction in the cleanup phase; for now the classic menu keeps its own Console prompts.
/// </summary>
internal static class TuiPrompts
{
    /// <summary>Yes/No confirmation. Returns true only if the user picks Yes.</summary>
    public static bool Confirm(IApplication app, string title, string message)
        => MessageBox.Query(app, title, message, "Yes", "No") == 0;

    /// <summary>
    /// Single-line text entry. Returns the entered text, or null if cancelled. Set
    /// <paramref name="secret"/> to mask the input (e.g. tokens).
    /// </summary>
    public static string PromptText(string title, string prompt, string initial = "", bool secret = false)
    {
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(70),
            Height = 9
        };

        var label = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Text = prompt
        };

        var field = new TextField
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Text = initial ?? string.Empty,
            Secret = secret
        };

        string result = null;

        var help = new Label
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(2),
            Text = "Enter: save   ·   Esc: cancel"
        };

        // A dialog button-bar steals focus from the TextField (making it untypable), so the field
        // handles the keys itself — Enter saves, Esc cancels — with the help line making that
        // clear. The field is then the only focusable view, so it reliably receives focus.
        field.KeyDown += (_, key) =>
        {
            if (key == Key.Enter)
            {
                result = field.Text;
                key.Handled = true;
                TuiHost.RequestStop();
            }
            else if (key == Key.Esc)
            {
                result = null;
                key.Handled = true;
                TuiHost.RequestStop();
            }
        };

        dialog.Add(label);
        dialog.Add(field);
        dialog.Add(help);
        dialog.Initialized += (_, _) => TuiHost.Invoke(() => field.SetFocus());

        TuiHost.Run(dialog);

        return result;
    }
}
