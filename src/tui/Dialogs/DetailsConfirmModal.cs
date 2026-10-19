using System.Collections.Generic;
using Pannella.Helpers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

public static class DetailsConfirmModal
{
    public static bool Confirm(string title, string description, IReadOnlyList<string> links,
        string confirmLabel = "_Install")
    {
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(75),
            Height = Dim.Percent(70)
        };

        View previous = null;

        void AddRow(View row)
        {
            row.X = 1;
            row.Y = previous == null ? 0 : Pos.Bottom(previous);
            dialog.Add(row);
            previous = row;
        }

        void AddText(string text, int height = 1) =>
            AddRow(new Label { Text = text ?? string.Empty, Width = Dim.Fill(2), Height = height, CanFocus = false });

        if (!string.IsNullOrWhiteSpace(description))
        {
            string wrapped = Util.WordWrap(description, 70);
            AddText(wrapped, wrapped.Split('\n').Length);
            AddText(string.Empty);
        }

        if (links is { Count: > 0 })
        {
            AddText("More info:");

            foreach (string link in links)
            {
                AddRow(new Link { Text = link, Url = link });
            }
        }

        bool confirmed = false;

        var yes = new Button { Text = confirmLabel, IsDefault = true };
        yes.Accepting += (_, e) =>
        {
            e.Handled = true;
            confirmed = true;
            TuiHost.RequestStop();
        };

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(yes);
        dialog.AddButton(cancel);

        TuiHost.Run(dialog);

        return confirmed;
    }
}
