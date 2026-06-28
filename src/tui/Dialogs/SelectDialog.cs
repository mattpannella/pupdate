using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Standard single-select modal popup: a titled dialog with a <see cref="MenuListView"/> where
/// Enter or a single click on an item returns its index; Cancel returns null. The single-select
/// counterpart to <see cref="ChecklistDialog"/>; reuse it for any "pick one" flow.
/// </summary>
public static class SelectDialog
{
    public static int? Show(string title, string hint, IReadOnlyList<string> labels)
    {
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(80),
            Height = Dim.Percent(80)
        };

        var hintLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = $"{hint}   (↑/↓ move · Enter/click selects · Cancel below)"
        };

        var list = new MenuListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        list.SetSource(new ObservableCollection<string>(labels));

        int? result = null;
        list.OnActivate(index =>
        {
            result = index;
            TuiHost.RequestStop();
        });

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = null;
            TuiHost.RequestStop();
        };

        dialog.AddButton(cancel);
        dialog.Add(hintLabel);
        dialog.Add(list);

        TuiHost.Run(dialog);

        return result;
    }
}
