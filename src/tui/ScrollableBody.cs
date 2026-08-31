using System;
using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// A stack of rows that scrolls when it outgrows its box. Modal content is otherwise clipped
/// silently - a Dialog shrinks its content area for the button bar, and anything past the fold just
/// disappears with no scrollbar and no error.
/// <para>
/// Rows are added top-to-bottom with <see cref="AddRow"/>; call <see cref="Finish"/> once to publish
/// the content height. Up/Down/PageUp/PageDown/Home/End move the viewport - Terminal.Gui 2.4.12
/// leaves <c>Command.ScrollUp</c>/<c>ScrollDown</c> unimplemented on a plain View, so this drives
/// <see cref="View.Viewport"/> directly.
/// </para>
/// </summary>
public sealed class ScrollableBody : View
{
    private View previous;
    private int rows;
    private int width = 1;

    public ScrollableBody()
    {
        CanFocus = true;
        VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        KeyDown += (_, key) => Scroll(key);

        // Neither a plain View nor TableView scrolls on the wheel in Terminal.Gui 2.4.12 (ListView
        // does), so translate it into the same clamped move the keys use.
        MouseEvent += (_, mouse) =>
        {
            bool down = mouse.Flags.HasFlag(MouseFlags.WheeledDown);

            if (down || mouse.Flags.HasFlag(MouseFlags.WheeledUp))
            {
                ScrollTo(Viewport.Y + (down ? 1 : -1));
                mouse.Handled = true;
            }
        };
    }

    /// <summary>Appends a row directly below the previous one.</summary>
    public void AddRow(View row, int height = 1)
    {
        row.X = 1;
        row.Y = previous == null ? 0 : Pos.Bottom(previous);
        row.Width = Dim.Fill(2);
        row.Height = height;

        Add(row);

        previous = row;
        rows += height;
    }

    /// <summary>Records how wide the widest row is, so horizontal content size is sane.</summary>
    public void Measure(string text) => width = Math.Max(width, (text?.Length ?? 0) + 2);

    /// <summary>Publishes the stacked height as the scrollable content size. Call once, after the
    /// last <see cref="AddRow"/>.</summary>
    public void Finish() => SetContentSize(new Size(width, rows));

    private void Scroll(Key key)
    {
        int page = Math.Max(1, Viewport.Height - 1);
        int max = Math.Max(0, GetContentSize().Height - Viewport.Height);

        int? target = null;

        if (key == Key.CursorDown) target = Viewport.Y + 1;
        else if (key == Key.CursorUp) target = Viewport.Y - 1;
        else if (key == Key.PageDown) target = Viewport.Y + page;
        else if (key == Key.PageUp) target = Viewport.Y - page;
        else if (key == Key.Home) target = 0;
        else if (key == Key.End) target = max;

        if (target == null)
        {
            return;
        }

        ScrollTo(target.Value);
        key.Handled = true;
    }

    private void ScrollTo(int y)
    {
        int max = Math.Max(0, GetContentSize().Height - Viewport.Height);

        Viewport = Viewport with { Y = Math.Clamp(y, 0, max) };
    }
}
