using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// The standard modal checklist popup: a titled dialog with a marking <see cref="MenuListView"/>
/// (Space toggles, hover, scrollbar) and OK/Cancel. Returns the set of checked indices, or null
/// if cancelled. Optionally enforces a maximum number of selections on confirm. Every "pick from
/// a list" dialog should be built on this rather than re-creating the scaffold.
/// </summary>
public static class ChecklistDialog
{
    public static HashSet<int> Show(
        string title,
        string hint,
        IReadOnlyList<string> labels,
        Func<int, bool> initialChecked,
        string okText = "OK",
        int? maxSelected = null)
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
            Text = $"{hint}   (↑/↓ move · Space toggles · {okText} / Cancel below)"
        };

        var list = new MenuListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ShowMarks = true,
            MarkMultiple = true
        };

        list.SetSource(new ObservableCollection<string>(labels));

        for (int i = 0; i < labels.Count; i++)
        {
            list.Source.SetMark(i, initialChecked(i));
        }

        HashSet<int> result = null;

        var ok = new Button { Text = $"_{okText}" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;

            var marked = new HashSet<int>(list.GetAllMarkedItems());

            if (maxSelected.HasValue && marked.Count > maxSelected.Value)
            {
                MessageBox.Query(dialog.App, "Too many",
                    $"Maximum is {maxSelected.Value}; you selected {marked.Count}. Unselect some.", "OK");
                return; // keep the dialog open
            }

            result = marked;
            TuiHost.RequestStop();
        };

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = null;
            TuiHost.RequestStop();
        };

        dialog.AddButton(ok);
        dialog.AddButton(cancel);
        dialog.Add(hintLabel);
        dialog.Add(list);

        TuiHost.Run(dialog);

        return result;
    }
}
