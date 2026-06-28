using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// A ListView preconfigured for menu/list use: focusable, an auto vertical scrollbar, and per-row
/// hover (the selection follows the mouse under the cursor). Centralizes the list setup that was
/// otherwise duplicated across every tab and dialog. Callers add their own activation (Enter /
/// single-click / Space) on top.
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

                if (row >= 0 && row < (Source?.Count ?? 0))
                {
                    SetSelection(row, false);
                }
            }
        };
    }
}
