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

                if (row >= 0 && row < Count)
                {
                    SetSelection(row, false);
                }
            }
        };
    }

    private int Count => Source?.Count ?? 0;

    /// <summary>
    /// Opt-in activation: a single click (on an item, deferred so the click first settles the
    /// selection) or Enter invokes <paramref name="onActivate"/> with the item index. Lists that
    /// have their own Enter semantics (e.g. Space-toggle + Save) simply don't call this.
    /// </summary>
    public void OnActivate(Action<int> onActivate)
    {
        MouseEvent += (_, mouse) =>
        {
            if (mouse.IsSingleClicked && mouse.Position is { } position)
            {
                int row = Viewport.Y + position.Y;

                if (row >= 0 && row < Count)
                {
                    TuiHost.Invoke(() => onActivate(row));
                }
            }
        };

        KeyDown += (_, key) =>
        {
            if (key == Key.Enter && SelectedItem is { } index && index >= 0 && index < Count)
            {
                onActivate(index);
                key.Handled = true;
            }
        };
    }
}
