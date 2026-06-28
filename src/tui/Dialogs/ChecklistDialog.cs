using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// The standard modal checklist popup: a titled dialog with a marking <see cref="MenuListView"/>
/// (Space toggles, hover, scrollbar) and OK/Cancel. Returns the set of checked indices (into the
/// original <paramref name="labels"/> list), or null if cancelled. Optionally enforces a maximum
/// number of selections on confirm. Every "pick from a list" dialog should be built on this.
///
/// Long lists support type-ahead filtering: with the list focused, typing letters narrows it to
/// matching rows (Backspace deletes, Esc clears the filter). Check state is tracked by original
/// index, so it survives filtering — toggle, filter to something else, toggle more, then OK.
///
/// Optionally a category filter (a dropdown-style button) narrows by a per-item key: pass
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

        // Checked state is keyed by ORIGINAL index so it survives filtering/re-sourcing.
        var checkedSet = new HashSet<int>();

        for (int i = 0; i < labels.Count; i++)
        {
            if (initialChecked(i))
            {
                checkedSet.Add(i);
            }
        }

        // Distinct category choices (key + display name), only if there's more than one to filter by.
        categoryDisplay ??= k => k;
        var categoryChoices = categories?
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .Select(k => (Key: k, Display: categoryDisplay(k)))
            .OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
        bool hasCategoryFilter = categoryChoices is { Count: > 1 };

        var hintLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        // Dropdown-style category filter button (opens a picker). null selection = all categories.
        string selectedCategory = null;
        Button filterButton = null;

        if (hasCategoryFilter)
        {
            filterButton = new Button
            {
                X = 0,
                Y = 1,
                Text = $"{categoryLabel}: All ▾"
            };
        }

        var list = new MenuListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ShowMarks = true,
            MarkMultiple = true
        };

        // Maps the current visible row -> original index in labels.
        var visibleToOriginal = new List<int>();
        string query = string.Empty;

        // Pull the current visible marks back into checkedSet before the source changes.
        void SyncMarksFromVisible()
        {
            if (list.Source == null || visibleToOriginal.Count == 0)
            {
                return;
            }

            var marked = new HashSet<int>(list.GetAllMarkedItems());

            for (int v = 0; v < visibleToOriginal.Count; v++)
            {
                int original = visibleToOriginal[v];

                if (marked.Contains(v))
                {
                    checkedSet.Add(original);
                }
                else
                {
                    checkedSet.Remove(original);
                }
            }
        }

        void Rebuild()
        {
            SyncMarksFromVisible();

            visibleToOriginal.Clear();
            var source = new ObservableCollection<string>();

            for (int i = 0; i < labels.Count; i++)
            {
                bool matchText = query.Length == 0 || labels[i].Contains(query, StringComparison.OrdinalIgnoreCase);
                bool matchCategory = selectedCategory == null
                    || (categories != null && i < categories.Count
                        && string.Equals(categories[i], selectedCategory, StringComparison.Ordinal));

                if (matchText && matchCategory)
                {
                    visibleToOriginal.Add(i);
                    source.Add(labels[i]);
                }
            }

            list.SetSource(source);

            for (int v = 0; v < visibleToOriginal.Count; v++)
            {
                list.Source.SetMark(v, checkedSet.Contains(visibleToOriginal[v]));
            }

            string filterNote = query.Length == 0
                ? $"type to filter · Space toggles · {okText} / Cancel below"
                : $"filter: \"{query}\"  ({visibleToOriginal.Count}/{labels.Count}) · Backspace edits · Esc clears";

            hintLabel.Text = $"{hint}   ({filterNote})";
        }

        if (filterButton != null)
        {
            filterButton.Accepting += (_, e) =>
            {
                e.Handled = true;

                var pickerLabels = new List<string> { "(All)" };
                pickerLabels.AddRange(categoryChoices.Select(c => c.Display));

                int? pick = SelectDialog.Show(categoryLabel, $"Filter by {categoryLabel.ToLower()}:", pickerLabels);

                if (pick == null)
                {
                    return;
                }

                selectedCategory = pick.Value == 0 ? null : categoryChoices[pick.Value - 1].Key;
                string display = pick.Value == 0 ? "All" : categoryChoices[pick.Value - 1].Display;
                filterButton.Text = $"{categoryLabel}: {display} ▾";
                Rebuild();
            };
        }

        HashSet<int> result = null;

        var ok = new Button { Text = $"_{okText}" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;
            SyncMarksFromVisible();

            if (maxSelected.HasValue && checkedSet.Count > maxSelected.Value)
            {
                MessageBox.Query(dialog.App, "Too many",
                    $"Maximum is {maxSelected.Value}; you selected {checkedSet.Count}. Unselect some.", "OK");
                return; // keep the dialog open
            }

            result = new HashSet<int>(checkedSet);
            TuiHost.RequestStop();
        };

        var cancel = new Button { Text = "_Cancel" };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = null;
            TuiHost.RequestStop();
        };

        // Type-ahead filtering, handled on the list itself so we don't need a focusable TextField
        // (which fights the dialog button-bar for focus). Space is left alone so it still toggles.
        list.KeyDown += (_, key) =>
        {
            if (key == Key.Backspace)
            {
                if (query.Length > 0)
                {
                    query = query.Substring(0, query.Length - 1);
                    Rebuild();
                    key.Handled = true;
                }
            }
            else if (key == Key.Esc)
            {
                if (query.Length > 0)
                {
                    query = string.Empty;
                    Rebuild();
                    key.Handled = true;
                }
            }
            else
            {
                var rune = key.AsRune;
                char c = (char)rune.Value;

                if (rune.Value > 32 && rune.Value < 0x10000 && !char.IsControl(c))
                {
                    query += c;
                    Rebuild();
                    key.Handled = true;
                }
            }
        };

        Rebuild();

        dialog.AddButton(ok);
        dialog.AddButton(cancel);
        dialog.Add(hintLabel);

        if (filterButton != null)
        {
            dialog.Add(filterButton);
        }

        dialog.Add(list);

        // Focus the list initially so Space/type-ahead work immediately (the filter button and the
        // OK/Cancel bar are otherwise reachable by Tab or mouse).
        dialog.Initialized += (_, _) => TuiHost.Invoke(() => list.SetFocus());

        TuiHost.Run(dialog);

        return result;
    }
}
