using System.Collections.ObjectModel;
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

    private readonly ObservableCollection<string> lines = new();
    private bool liveActive;
    private string buffer = string.Empty;

    public LogView()
    {
        CanFocus = true;
        SetSource(lines);
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
            lines[lines.Count - 1] = text;
        }
        else
        {
            AddRow(text);
        }
    }

    private void AddRow(string text)
    {
        lines.Add(text);

        while (lines.Count > MaxLines)
        {
            lines.RemoveAt(0);
        }
    }

    private static string StripCarriage(string s)
    {
        int cr = s.LastIndexOf('\r');
        return cr >= 0 ? s.Substring(cr + 1) : s;
    }
}
