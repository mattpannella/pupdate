using System.Text;
using System.Threading;
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

    // Disables Terminal.Gui's "_x" hotkey parsing so text from plugins shows underscores as typed.
    internal static readonly Rune NoHotKey = (Rune)0xFFFF;

    /// <summary>
    /// Single-line text entry. Returns the entered text, or null if cancelled. Set
    /// <paramref name="secret"/> to mask the input (e.g. tokens). <paramref name="forPlugin"/>
    /// docks a compact dialog at the top (the plugin's output stays visible below it), shows the
    /// question once as the title (pass a null <paramref name="prompt"/>) and renders it literally.
    /// </summary>
    public static string PromptText(string title, string prompt, string initial = "", bool secret = false,
        bool forPlugin = false)
    {
        // The button bar lives in the dialog's Padding and reserves the bottom by SHRINKING the
        // content area (ContentSize); anything below it is silently clipped but stays focusable.
        // Measured against Terminal.Gui 2.4.12 with TuiHost.Run's Thickness(1, 3, 1, 1) border:
        // Height 10 yields only 2 content rows (the field's bottom border is cut off), 12 is the
        // first that fits the label + 3-row bordered field. 13 keeps a row of slack so a future
        // chrome change can't silently clip the field again.
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(forPlugin ? 85 : 70),
            Height = prompt == null ? 12 : 13
        };

        if (forPlugin)
        {
            dialog.Y = 0;
            dialog.HotKeySpecifier = NoHotKey;
        }

        var label = prompt == null ? null : new Label
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Text = prompt,
            HotKeySpecifier = forPlugin ? NoHotKey : (Rune)'_'
        };

        var field = new TextField
        {
            X = 1,
            Y = label == null ? 0 : 1,
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

        if (label != null)
        {
            dialog.Add(label);
        }

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
    
    public static T FromBackground<T>(Func<T> show)
    {
        T result = default;
        using var done = new ManualResetEventSlim(false);

        TuiHost.Invoke(() =>
        {
            try
            {
                result = show();
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();

        return result;
    }
}
