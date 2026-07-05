using Terminal.Gui.App;
using Terminal.Gui.Drawing;
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
        // The button bar reserves the bottom of the dialog by SHRINKING the content area
        // (ContentSize); anything placed below it is silently clipped but stays focusable.
        // Height 10 leaves exactly 4 content rows: the label + the 3-row bordered field.
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(70),
            Height = 10
        };

        var label = new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Text = prompt
        };

        var field = new TextField
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = 3,
            Text = initial ?? string.Empty,
            Secret = secret,
            BorderStyle = LineStyle.Single
        };

        string result = null;

        // Enter/Esc work directly from the field; Save/Cancel cover the mouse/Tab path.
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

        var save = new Button { Text = "_Save", IsDefault = true };
        save.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = field.Text;
            TuiHost.RequestStop();
        };

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = null;
            TuiHost.RequestStop();
        };

        dialog.Add(label);
        dialog.Add(field);
        dialog.AddButton(save);
        dialog.AddButton(cancel);

        // Grab focus on the first loop iteration, after the dialog has set its own initial focus
        // (an immediate Invoke runs during init and gets overridden).
        dialog.Initialized += (_, _) => TuiHost.AddTimeout(TimeSpan.Zero, () =>
        {
            field.SetFocus();
            return false;
        });

        TuiHost.Run(dialog);

        return result;
    }
}
