using System;
using System.Collections.Generic;
using Terminal.Gui.Input;

namespace Pannella.TUI;

internal sealed class SettingsListView : MenuListView
{
    private IReadOnlyList<SettingsRow> rows = Array.Empty<SettingsRow>();
    private Action<int> activated;

    public SettingsListView()
    {
        KeyDown += (_, key) =>
        {
            if (key == Key.CursorUp)
            {
                Step(-1);
            }
            else if (key == Key.CursorDown)
            {
                Step(1);
            }
            else if (key == Key.Home)
            {
                Select(NextSelectable(0, 1));
            }
            else if (key == Key.End)
            {
                Select(NextSelectable(rows.Count - 1, -1));
            }
            else if (key == Key.PageUp)
            {
                Page(-1);
            }
            else if (key == Key.PageDown)
            {
                Page(1);
            }
            else if (key == Key.Space)
            {
                if (SelectedItem is { } index && index >= 0 && index < rows.Count && rows[index].Selectable)
                {
                    activated?.Invoke(index);
                }
            }
            else
            {
                return;
            }

            key.Handled = true;
        };
    }

    public void SetRows(IReadOnlyList<SettingsRow> value) => rows = value;

    /// <summary>Wires Enter, a single click, and Space to <paramref name="onActivate"/>.</summary>
    public void SetActivate(Action<int> onActivate)
    {
        activated = onActivate;
        OnActivate(onActivate);
    }

    /// <summary>Highlights the first non-header row.</summary>
    public void SelectFirst() => Select(NextSelectable(0, 1));

    protected override bool CanSelect(int row) => row >= 0 && row < rows.Count && rows[row].Selectable;

    private void Step(int direction) => Select(NextSelectable((SelectedItem ?? 0) + direction, direction));

    private void Page(int direction)
    {
        int target = Math.Clamp((SelectedItem ?? 0) + direction * Math.Max(1, Viewport.Height - 1), 0, rows.Count - 1);

        // Prefer a row further along in the direction of travel; fall back the other way at the ends.
        Select(NextSelectable(target, direction) is var forward && forward >= 0
            ? forward
            : NextSelectable(target, -direction));
    }

    private int NextSelectable(int from, int direction)
    {
        for (int i = from; i >= 0 && i < rows.Count; i += direction)
        {
            if (rows[i].Selectable)
            {
                return i;
            }
        }

        return -1;
    }

    private void Select(int index)
    {
        if (index >= 0)
        {
            SetSelection(index, false);
            EnsureSelectedItemVisible();
        }
    }
}
