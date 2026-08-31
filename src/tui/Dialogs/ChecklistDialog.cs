using System.Collections.Generic;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// The standard modal checklist popup: a titled dialog wrapping a <see cref="MarkableFilterList"/>
/// (Space toggles, type-ahead filter, optional category dropdown) plus OK/Cancel. Returns the set of
/// checked indices (into the original <paramref name="labels"/> list), or null if cancelled.
/// Optionally enforces a maximum number of selections on confirm. Every "pick from a list" dialog
/// should be built on this.
///
/// Check state is tracked by original index, so it survives filtering - toggle, filter to something
/// else, toggle more, then OK.
///
/// Optionally a category filter (a dropdown) narrows by a per-item key: pass
/// <paramref name="categories"/> (one key per label, e.g. a platform_id), an optional
/// <paramref name="categoryDisplay"/> to map keys to friendly names, and a
/// <paramref name="categoryLabel"/> (e.g. "Platform"). The text and category filters compose.
/// </summary>
public static class ChecklistDialog
{
    public static HashSet<int> Show(
        string title,
        string hint,
        IReadOnlyList<string> labels,
        Func<int, bool> initialChecked,
        string okText = "OK",
        int? maxSelected = null,
        IReadOnlyList<string> categories = null,
        Func<string, string> categoryDisplay = null,
        string categoryLabel = "Filter")
    {
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(80),
            Height = Dim.Percent(80)
        };

        var panel = new MarkableFilterList
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var filters = categories != null
            ? new[] { new MarkableFilterList.FilterSpec(categories, categoryLabel, categoryDisplay) }
            : null;

        panel.SetItems(labels, initialChecked, hint, filters, actionHint: $"{okText} / Cancel below");

        HashSet<int> result = null;

        var ok = new Button { Text = $"_{okText}" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;
            var chosen = panel.CheckedOriginalIndices;

            if (maxSelected.HasValue && chosen.Count > maxSelected.Value)
            {
                MessageBox.Query(dialog.App, "Too many",
                    $"Maximum is {maxSelected.Value}; you selected {chosen.Count}. Unselect some.", "OK");
                return; // keep the dialog open
            }

            result = chosen;
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
        dialog.Add(panel);

        // Focus the list initially so Space/type-ahead work immediately (the filter button and the
        // OK/Cancel bar are otherwise reachable by Tab or mouse).
        dialog.Initialized += (_, _) => TuiHost.Invoke(() => panel.FocusList());

        TuiHost.Run(dialog);

        return result;
    }
}
