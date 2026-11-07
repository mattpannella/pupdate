using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq;
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
    // forPlugin: a short list docks at the top, sized to its items, so the plugin's output stays
    // visible below; the title is shown literally (no "_x" hotkey parsing).
    public static int? Show(string title, string hint, IReadOnlyList<string> labels, bool forPlugin = false)
    {
        bool compact = forPlugin && labels.Count <= 10;

        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(80),
            Height = compact ? labels.Count + 12 : Dim.Percent(80)
        };

        if (compact)
        {
            dialog.Y = 0;
        }

        var hintLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = $"{hint}   (↑/↓ move · Enter/click/[key] selects · Cancel below)"
        };

        if (forPlugin)
        {
            dialog.HotKeySpecifier = TuiPrompts.NoHotKey;
            hintLabel.HotKeySpecifier = TuiPrompts.NoHotKey;
        }

        var list = new MenuListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        // Prefix each option with its accelerator ("[0] …", then "[G] …").
        list.SetSource(new ObservableCollection<string>(
            labels.Select((label, i) => TuiAccelerators.FormatItem(i, label))));

        int? result = null;
        list.OnActivate(index =>
        {
            result = index;
            TuiHost.RequestStop();
        }, numbered: true);

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
