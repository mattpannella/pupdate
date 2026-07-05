using System.Text.RegularExpressions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Shown once at startup: the support/donation message that the classic menu pins at the top of its
/// header (a randomly-picked installed core's funding links). URLs render as clickable
/// <see cref="Link"/>s that open the default browser. Dismissed with Continue / Esc.
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

        var dialog = new Dialog
        {
            Title = "Support the Developers",
            Width = Dim.Percent(80),
            Height = Dim.Percent(55)
        };

        // URL lines become clickable links (so no blanket Enter-dismiss — Enter belongs to the
        // focused link); Esc / Continue close the dialog.
        View previous = null;

        foreach (string raw in sponsor.TrimEnd().Split('\n'))
        {
            string line = raw.Trim();

            View row = Regex.IsMatch(line, @"^https?://\S+$")
                ? new Link { Url = line }
                : new Label { Text = raw.TrimEnd(), Width = Dim.Fill(), Height = 1, CanFocus = false };

            row.X = 0;
            row.Y = previous == null ? 0 : Pos.Bottom(previous);

            dialog.Add(row);
            previous = row;
        }

        var ok = new Button { Text = "_Continue" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(ok);

        TuiHost.Run(dialog);
    }
}
