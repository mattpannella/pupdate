using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Modal replacement for the paginated ConsoleMenu core checklist (ShowCoresMenu). A marking
/// ListView (Space toggles) with an auto scrollbar — no pagination. Returns a map of
/// core id -> wanted-enabled, or null if the user cancels, matching the shape RunCoreSelector
/// consumed so the downstream EnableCore/DisableCore logic is unchanged.
/// </summary>
public static class CoreSelectorDialog
{
    public static Dictionary<string, bool> Show(IReadOnlyList<Core> cores, string message)
    {
        var dialog = new Dialog
        {
            Title = "Select Cores",
            Width = Dim.Percent(80),
            Height = Dim.Percent(80)
        };

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = $"{message}   (↑/↓ move · Space toggles · Save / Cancel below)"
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

        // The mark (checkbox) is shown by the ListView, so the label only needs the id plus a
        // license hint. A core is "on" when it is NOT skipped.
        var labels = new ObservableCollection<string>(
            cores.Select(c => c.requires_license ? $"{c.id}  (license required)" : c.id));

        list.SetSource(labels);

        for (int i = 0; i < cores.Count; i++)
        {
            list.Source.SetMark(i, !ServiceHelper.SettingsService.GetCoreSettings(cores[i].id).skip);
        }

        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        bool saved = false;

        var save = new Button { Text = "_Save" };
        save.Accepting += (_, e) =>
        {
            e.Handled = true;
            saved = true;
            TuiHost.RequestStop();
        };

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(save);
        dialog.AddButton(cancel);
        dialog.Add(hint);
        dialog.Add(list);

        // Modal nested run; returns when Save/Cancel calls RequestStop.
        TuiHost.Run(dialog);

        if (!saved)
        {
            return null;
        }

        var marked = new HashSet<int>(list.GetAllMarkedItems());
        var result = new Dictionary<string, bool>();

        for (int i = 0; i < cores.Count; i++)
        {
            result[cores[i].id] = marked.Contains(i);
        }

        return result;
    }
}
