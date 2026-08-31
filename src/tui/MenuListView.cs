using System;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// A ListView preconfigured for menu/list use: focusable, an auto vertical scrollbar, and per-row
/// hover (the selection follows the mouse). Centralizes the list setup duplicated across tabs and
/// dialogs. Call <see cref="OnActivate"/> to opt into run-the-item behavior (single click or Enter).
/// </summary>
public class MenuListView : ListView
{
    public MenuListView()
    {
        CanFocus = true;
        VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        MousePositionTracking = true;

        // Per-row hover: Viewport.Y is the scroll offset, Position.Y the row within the view.
        MouseEvent += (_, mouse) =>
        {
            if (mouse.Position is { } position)
            {
                int row = Viewport.Y + position.Y;

                if (row >= 0 && row < Count && CanSelect(row))
                {
                    SetSelection(row, false);
                }
            }
        };

        // Up at the first row / Down at the last must stop here. An unhandled cursor key bubbles
        // into Terminal.Gui's focus navigation, which walks out of the list and switches tabs -
        // losing whatever the tab had staged (issue #517).
        KeyDown += (_, key) =>
        {
            if ((key == Key.CursorUp && (SelectedItem ?? 0) <= 0)
                || (key == Key.CursorDown && (SelectedItem ?? 0) >= Count - 1))
            {
                key.Handled = true;
            }
        };
    }

    private int Count => Source?.Count ?? 0;

    /// <summary>
    /// Whether a row can be highlighted/activated. Override to make a list skip non-items (e.g. the
    /// Settings tab's group headers); the default list has no such rows.
    /// </summary>
    protected virtual bool CanSelect(int row) => true;

    /// <summary>
    /// Opt-in activation: a single click (on an item, deferred so the click first settles the
    /// selection) or Enter invokes <paramref name="onActivate"/> with the item index. Lists that
    /// have their own Enter semantics (e.g. Space-toggle + Save) simply don't call this.
    /// When <paramref name="numbered"/> is set, item-key accelerators (0-9 then G-Z) also run the
    /// matching item - used by modal popups, where the shell's global accelerator stands down.
    /// </summary>
    public void OnActivate(Action<int> onActivate, bool numbered = false)
    {
        MouseEvent += (_, mouse) =>
        {
            if (mouse.IsSingleClicked && mouse.Position is { } position)
            {
                int row = Viewport.Y + position.Y;

                if (row >= 0 && row < Count && CanSelect(row))
                {
                    TuiHost.Invoke(() => onActivate(row));
                }
            }
        };

        KeyDown += (_, key) =>
        {
            if (key == Key.Enter && SelectedItem is { } index && index >= 0 && index < Count && CanSelect(index))
            {
                onActivate(index);
                key.Handled = true;
                return;
            }

            if (numbered && !key.IsCtrl && !key.IsAlt && key.AsRune.Value != 0)
            {
                int target = TuiAccelerators.ItemIndex((char)key.AsRune.Value);

                if (target >= 0 && target < Count && CanSelect(target))
                {
                    SetSelection(target, false);
                    onActivate(target);
                    key.Handled = true;
                }
            }
        };
    }
}
