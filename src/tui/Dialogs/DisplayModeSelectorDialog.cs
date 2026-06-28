using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pannella.Models.DisplayModes;
using Pannella.Services;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Modal replacement for the paginated display-mode checklist. Marking ListView (Space toggles)
/// with a scrollbar; the 16-max cap is enforced on Apply. Returns the selected display-mode
/// values, or null if cancelled.
/// </summary>
public static class DisplayModeSelectorDialog
{
    public static List<string> Show(IReadOnlyList<DisplayMode> modes)
    {
        var dialog = new Dialog
        {
            Title = "Display Modes",
            Width = Dim.Percent(80),
            Height = Dim.Percent(80)
        };

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = $"Select up to {CoresService.DISPLAY_MODES_MAX}.  (↑/↓ move · Space toggles · Apply / Cancel below)"
        };

        var list = new ListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = true,
            ShowMarks = true,
            MarkMultiple = true
        };

        // Selection starts empty, matching the classic menu.
        list.SetSource(new ObservableCollection<string>(modes.Select(m => m.description)));
        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        List<string> result = null;

        var apply = new Button { Text = "_Apply" };
        apply.Accepting += (_, e) =>
        {
            e.Handled = true;

            var marked = list.GetAllMarkedItems().ToList();

            if (marked.Count > CoresService.DISPLAY_MODES_MAX)
            {
                MessageBox.Query(dialog.App, "Too many",
                    $"Maximum is {CoresService.DISPLAY_MODES_MAX}; you selected {marked.Count}. Unselect some.",
                    "OK");
                return; // keep the dialog open
            }

            result = marked.Select(i => modes[i].value).ToList();
            TuiHost.RequestStop();
        };

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = null;
            TuiHost.RequestStop();
        };

        dialog.AddButton(apply);
        dialog.AddButton(cancel);
        dialog.Add(hint);
        dialog.Add(list);

        TuiHost.Run(dialog);

        return result;
    }
}
