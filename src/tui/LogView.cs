using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Terminal.Gui.Text;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// A scrollable, bounded log list with terminal-style output. <see cref="AppendLine"/> adds a row;
/// <see cref="WriteRaw"/> honors \n (commit the current line) and \r (overwrite the current "live"
/// line in place) so progress-bar style output collapses to one updating row instead of many.
/// Reused by the Status pane and the plugin-run modal.
/// </summary>
public sealed class LogView : ListView
{
    private const int MaxLines = 2000;

    // Trim in batches so we aren't shifting the whole list on every append once full.
    private const int TrimBatch = 200;

    private readonly LogSource lines = new();
    private bool liveActive;
    private string buffer = string.Empty;

    public LogView()
    {
        CanFocus = true;
        Source = lines;
        VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
    }

    /// <summary>Adds a finished row.</summary>
    public void AppendLine(string message)
    {
        AddRow(message ?? string.Empty);
        liveActive = false;
        MoveEnd(false);
    }

    /// <summary>
    /// Appends raw output, honoring \n (commit a line) and \r (overwrite the live line in place).
    /// </summary>
    public void WriteRaw(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        buffer += text;

        int newline;
        while ((newline = buffer.IndexOf('\n')) >= 0)
        {
            CommitOrReplace(StripCarriage(buffer.Substring(0, newline)));
            liveActive = false;
            buffer = buffer.Substring(newline + 1);
        }

        // Whatever remains is a partial (live) line; collapse any carriage returns within it.
        buffer = StripCarriage(buffer);

        if (buffer.Length > 0)
        {
            CommitOrReplace(buffer);
            liveActive = true;
        }

        MoveEnd(false);
    }

    private void CommitOrReplace(string text)
    {
        if (liveActive && lines.Count > 0)
        {
            lines.ReplaceLast(text);
        }
        else
        {
            AddRow(text);
        }
    }

    private void AddRow(string text)
    {
        lines.Add(text);

        if (lines.Count > MaxLines + TrimBatch)
        {
            lines.RemoveFirst(lines.Count - MaxLines);
        }
    }

    private static string StripCarriage(string s)
    {
        int cr = s.LastIndexOf('\r');
        return cr >= 0 ? s.Substring(cr + 1) : s;
    }

    // ListWrapper re-measures every item on each change, which makes appends O(n) on the UI
    // thread; a long run (e.g. Reinstall All) backs up the Invoke queue and the pane looks hung.
    // This keeps the widest-line value incrementally instead.
    private sealed class LogSource : IListDataSource
    {
        private readonly List<string> items = new();
        private readonly List<int> widths = new();
        private int maxWidth;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public int Count => items.Count;

        public int MaxItemLength => maxWidth;

        public bool SuspendCollectionChangedEvent { get; set; }

        public void Add(string text)
        {
            int width = text.GetColumns();

            items.Add(text);
            widths.Add(width);
            maxWidth = System.Math.Max(maxWidth, width);
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, text, items.Count - 1));
        }

        public void ReplaceLast(string text)
        {
            int index = items.Count - 1;
            string old = items[index];
            int width = text.GetColumns();

            items[index] = text;
            widths[index] = width;
            maxWidth = width >= maxWidth ? width : widths.Max();
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, text, old, index));
        }

        public void RemoveFirst(int count)
        {
            items.RemoveRange(0, count);
            widths.RemoveRange(0, count);
            maxWidth = widths.Count > 0 ? widths.Max() : 0;
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
        {
            listView.Move(col, row);

            string text = items[item];
            int used = 0;
            int skipped = 0;

            foreach (System.Text.Rune rune in text.EnumerateRunes())
            {
                int columns = System.Math.Max(rune.GetColumns(), 0);

                if (skipped < viewportX)
                {
                    skipped += columns;
                    continue;
                }

                if (used + columns > width)
                {
                    break;
                }

                listView.AddRune(rune);
                used += columns;
            }

            if (used < width)
            {
                listView.AddStr(new string(' ', width - used));
            }
        }

        public bool RenderMark(ListView listView, int item, int row, bool isMarked, bool allowsMultiple) => false;

        public bool IsMarked(int item) => false;

        public void SetMark(int item, bool value)
        {
        }

        public IList ToList() => items;

        public void Dispose()
        {
        }

        private void Raise(NotifyCollectionChangedEventArgs args)
        {
            if (!SuspendCollectionChangedEvent)
            {
                CollectionChanged?.Invoke(this, args);
            }
        }
    }
}
