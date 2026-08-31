using Pannella.Helpers;
using Pannella.Models.Extras;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Description + links for a Pocket Extra, shown before installing when the
/// <c>show_menu_descriptions</c> setting is on - the TUI counterpart of the blurb the classic menu
/// prints in its Pocket Extras flow. Returns true if the user chose to install.
/// </summary>
public static class ExtraDetailsModal
{
    public static bool Confirm(PocketExtra extra, string title)
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

        void AddLink(string url) => AddRow(new Link { Text = url, Url = url });

        if (!string.IsNullOrWhiteSpace(extra.description))
        {
            string wrapped = Util.WordWrap(extra.description, 70);
            AddText(wrapped, wrapped.Split('\n').Length);
            AddText(string.Empty);
        }

        if (!string.IsNullOrEmpty(extra.github_user) && !string.IsNullOrEmpty(extra.github_repository))
        {
            AddText("More info:");
            AddLink($"https://github.com/{extra.github_user}/{extra.github_repository}");
        }

        if (extra.additional_links is { Count: > 0 })
        {
            foreach (string link in extra.additional_links)
            {
                AddLink(link);
            }
        }

        bool install = false;

        var yes = new Button { Text = "_Install", IsDefault = true };
        yes.Accepting += (_, e) =>
        {
            e.Handled = true;
            install = true;
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

        return install;
    }
}
