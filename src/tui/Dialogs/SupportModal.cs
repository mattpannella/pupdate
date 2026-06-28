using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Shown once at startup: the support/donation message that the classic menu pins at the top of its
/// header (a randomly-picked installed core's funding links). Dismissed with Continue / Enter / Esc.
/// No-op when no installed core advertises funding — there's nothing to show.
/// </summary>
public static class SupportModal
{
    public static void Show()
    {
        string sponsor = Program.GetRandomSponsorLinks();

        if (string.IsNullOrWhiteSpace(sponsor))
        {
            return;
        }

        string body = sponsor.TrimEnd();

        var dialog = new Dialog
        {
            Title = "Support the Developers",
            Width = Dim.Percent(80),
            Height = Dim.Percent(55)
        };

        var text = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Text = body,
            CanFocus = false
        };

        var ok = new Button { Text = "_Continue" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        // Enter or Esc also dismisses (the message is informational).
        dialog.KeyDown += (_, key) =>
        {
            if (key == Key.Enter || key == Key.Esc)
            {
                key.Handled = true;
                TuiHost.RequestStop();
            }
        };

        dialog.AddButton(ok);
        dialog.Add(text);

        TuiHost.Run(dialog);
    }
}
