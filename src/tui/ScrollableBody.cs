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

        Viewport = Viewport with { Y = Math.Clamp(target.Value, 0, max) };
        key.Handled = true;
    }
}
