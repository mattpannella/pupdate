using System;
using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// The persistent live status pane: a progress bar over an auto-scrolling, read-only log.
/// It is the single sink for the <c>StatusUpdated</c> stream and the download progress event
/// while the TUI owns the screen. All mutators must be called on the UI thread
/// (callers marshal via <c>Application.Invoke</c>).
/// </summary>
public sealed class StatusPane : FrameView
{
    private const int MaxLines = 2000;

    private readonly ProgressBar progress;
    private readonly ListView log;
    private readonly ObservableCollection<string> lines = new();

    public StatusPane()
    {
        Title = "Status";

        progress = new ProgressBar
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Fraction = 0f
        };

        log = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        log.SetSource(lines);

        Add(progress);
        Add(log);
    }

    public void AppendLine(string message)
    {
        lines.Add(message ?? string.Empty);

        // Bound memory on long-running sessions; drop the oldest lines.
        while (lines.Count > MaxLines)
        {
            lines.RemoveAt(0);
        }

        // Keep the newest line in view.
        log.MoveEnd(false);
    }

    public void SetProgress(double fraction)
    {
        progress.Fraction = (float)Math.Clamp(fraction, 0d, 1d);
    }
}
