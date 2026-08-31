using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// A reusable filterable, markable list: a marking <see cref="MenuListView"/> (Space/click toggles)
/// with type-ahead text filtering and any number of category dropdowns (each narrows by a per-item
/// key; they compose with each other and with the text filter). Check state is tracked by ORIGINAL
/// index, so it survives filtering. Hosts (the <see cref="ChecklistDialog"/> popup and the Cores tab)
/// drop this in and add their own buttons / controls around it.
///
/// Layout inside the view: a hint label (row 0), one row per category dropdown, and the list filling
/// the rest. Call <see cref="SetItems"/> to populate.
/// </summary>
public sealed class MarkableFilterList : View
{
    /// <summary>Describes one category dropdown: a per-label key list, a display-name mapper, and a label.</summary>
    public sealed class FilterSpec
    {
        public IReadOnlyList<string> Keys { get; }
        public Func<string, string> Display { get; }
        public string Label { get; }

        public FilterSpec(IReadOnlyList<string> keys, string label, Func<string, string> display = null)
        {
            Keys = keys;
            Label = string.IsNullOrEmpty(label) ? "Filter" : label;
            Display = display ?? (k => k);
        }
    }

    private sealed class FilterState
    {
        public IReadOnlyList<string> Keys;
        public List<(string Key, string Display)> Choices;
        public List<string> ChoiceLabels;
        public Label LabelView;
        public DropDownList Dropdown;
        public string Selected;
    }

    private readonly Label hintLabel;
    private readonly MenuListView list;
    private readonly List<FilterState> filters = new();

    private IReadOnlyList<string> labels = Array.Empty<string>();
    private string hint = string.Empty;
    private string actionHint;

    private readonly List<int> visibleToOriginal = new();
    private readonly HashSet<int> checkedSet = new();
    private string query = string.Empty;

    /// <summary>Raised when the highlighted row is activated with Enter; the argument is the ORIGINAL index.</summary>
    public event Action<int> ItemActivated;

    /// <param name="enterActivates">When true, pressing Enter on the highlighted row raises
    /// <see cref="ItemActivated"/>. Single click is left to the list, which toggles the mark - so a
    /// click checks/unchecks the row, it does not activate. Leave false for checklists whose Enter
    /// belongs to a default button.</param>
    public MarkableFilterList(bool enterActivates = false)
    {
        CanFocus = true;

        hintLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        list = new MenuListView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ShowMarks = true,
            MarkMultiple = true
        };

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

        list.MouseEvent += (_, mouse) =>
        {
            if (mouse.IsSingleClicked && mouse.Position is { } pos)
            {
                int row = list.Viewport.Y + pos.Y;

                if (row >= 0 && row < visibleToOriginal.Count)
                {
                    list.SetSelection(row, false);
                    ToggleVisibleRow(row);
                    mouse.Handled = true;
                }
            }
        };

        if (enterActivates)
        {
            list.KeyDown += (_, key) =>
            {
                if (key == Key.Enter && HighlightedOriginalIndex is { } original)
                {
                    ItemActivated?.Invoke(original);
                    key.Handled = true;
                }
            };
        }

        Add(hintLabel);
        Add(list);
    }

    /// <summary>
    /// Populate (or repopulate) the list. <paramref name="initialChecked"/> is queried per original
    /// index. Each <see cref="FilterSpec"/> in <paramref name="filters"/> becomes a dropdown (shown
    /// only when it has more than one distinct key); they compose with each other and the text filter.
    /// <paramref name="actionHint"/> is appended to the hint (e.g. "Enter = details").
    /// </summary>
    public void SetItems(
        IReadOnlyList<string> labels,
        Func<int, bool> initialChecked,
        string hint = "",
        IReadOnlyList<FilterSpec> filters = null,
        string actionHint = null)
    {
        this.labels = labels ?? Array.Empty<string>();
        this.hint = hint ?? string.Empty;
        this.actionHint = actionHint;

        checkedSet.Clear();

        for (int i = 0; i < this.labels.Count; i++)
        {
            if (initialChecked != null && initialChecked(i))
            {
                checkedSet.Add(i);
            }
        }

        BuildFilters(filters);
        Rebuild();
    }

    /// <summary>The set of checked ORIGINAL indices (into the labels passed to <see cref="SetItems"/>).</summary>
    public HashSet<int> CheckedOriginalIndices
    {
        get
        {
            SyncMarksFromVisible();
            return new HashSet<int>(checkedSet);
        }
    }

    /// <summary>
    /// The ORIGINAL index of the highlighted row, falling back to the first visible row so callers
    /// (Enter / a Details button) always have a target when the list is non-empty. Null only when the
    /// list is empty.
    /// </summary>
    public int? HighlightedOriginalIndex
    {
        get
        {
            if (list.SelectedItem is { } v && v >= 0 && v < visibleToOriginal.Count)
            {
                return visibleToOriginal[v];
            }

            return visibleToOriginal.Count > 0 ? visibleToOriginal[0] : null;
        }
    }

    /// <summary>Check every currently-visible (filtered) row.</summary>
    public void SelectAllVisible() => SetVisibleMarks(true);

    /// <summary>Uncheck every currently-visible (filtered) row.</summary>
    public void ClearAllVisible() => SetVisibleMarks(false);

    /// <summary>Give keyboard focus to the list (so Space / type-ahead work immediately).</summary>
    public void FocusList() => list.SetFocus();

    private void BuildFilters(IReadOnlyList<FilterSpec> specs)
    {
        foreach (var f in filters)
        {
            Remove(f.LabelView);
            Remove(f.Dropdown);
        }

        filters.Clear();

        int row = 1;

        foreach (var spec in specs ?? Array.Empty<FilterSpec>())
        {
            var choices = spec.Keys?
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .Select(k => (Key: k, Display: spec.Display(k)))
                .OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (choices is not { Count: > 1 })
            {
                continue;
            }

            var choiceLabels = new List<string> { "(All)" };
            choiceLabels.AddRange(choices.Select(c => c.Display));

            var labelView = new Label
            {
                X = 0,
                Y = row,
                Text = $"{spec.Label}:"
            };

            var dropdown = new DropDownList
            {
                X = Pos.Right(labelView) + 1,
                Y = row,
                Source = new ListWrapper<string>(new ObservableCollection<string>(choiceLabels)),
                Value = "(All)"
            };

            // A closed DropDownList binds ↑/↓ to cycling its own selection, trapping the cursor after a
            // pick, drop those; arrows belong to the list.
            dropdown.KeyBindings.Remove(Key.CursorUp);
            dropdown.KeyBindings.Remove(Key.CursorDown);

            var state = new FilterState
            {
                Keys = spec.Keys,
                Choices = choices,
                ChoiceLabels = choiceLabels,
                LabelView = labelView,
                Dropdown = dropdown
            };

            dropdown.ValueChanged += (_, _) =>
            {
                int index = state.ChoiceLabels.IndexOf(dropdown.Text);
                state.Selected = index <= 0 ? null : state.Choices[index - 1].Key;
                Rebuild();

                // The closing popover re-focuses the dropdown AFTER this event, so queue the move.
                TuiHost.AddTimeout(TimeSpan.Zero, () =>
                {
                    list.SetFocus();
                    return false;
                });
            };

            filters.Add(state);
            Add(labelView);
            Add(dropdown);
            row++;
        }

        list.Y = 1 + Math.Max(1, filters.Count);
    }

    // Pull the current visible marks back into checkedSet before the source changes.
    private void SyncMarksFromVisible()
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

    private void Rebuild()
    {
        SyncMarksFromVisible();

        visibleToOriginal.Clear();
        var source = new ObservableCollection<string>();

        for (int i = 0; i < labels.Count; i++)
        {
            bool matchText = query.Length == 0 || labels[i].Contains(query, StringComparison.OrdinalIgnoreCase);

            if (matchText && MatchesFilters(i))
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

        if (source.Count > 0)
        {
            if (list.SelectedItem is not { } s || s >= source.Count)
            {
                list.SelectedItem = 0;
            }
        }

        string note = query.Length == 0
            ? $"type to filter · click/Space toggles{(string.IsNullOrEmpty(actionHint) ? "" : $" · {actionHint}")}"
            : $"filter: \"{query}\"  ({visibleToOriginal.Count}/{labels.Count}) · Backspace edits · Esc clears";

        hintLabel.Text = string.IsNullOrEmpty(hint) ? $"({note})" : $"{hint}   ({note})";
    }

    private bool MatchesFilters(int index)
    {
        foreach (var f in filters)
        {
            if (f.Selected == null)
            {
                continue;
            }

            bool ok = f.Keys != null && index < f.Keys.Count
                && string.Equals(f.Keys[index], f.Selected, StringComparison.Ordinal);

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private void ToggleVisibleRow(int row)
    {
        if (list.Source == null || row < 0 || row >= visibleToOriginal.Count)
        {
            return;
        }

        bool marked = !list.Source.IsMarked(row);
        list.Source.SetMark(row, marked);

        int original = visibleToOriginal[row];

        if (marked)
        {
            checkedSet.Add(original);
        }
        else
        {
            checkedSet.Remove(original);
        }

        list.SetNeedsDraw();
    }

    private void SetVisibleMarks(bool marked)
    {
        if (list.Source == null)
        {
            return;
        }

        for (int v = 0; v < visibleToOriginal.Count; v++)
        {
            if (marked)
            {
                checkedSet.Add(visibleToOriginal[v]);
            }
            else
            {
                checkedSet.Remove(visibleToOriginal[v]);
            }

            list.Source.SetMark(v, marked);
        }

        list.SetNeedsDraw();
    }
}
