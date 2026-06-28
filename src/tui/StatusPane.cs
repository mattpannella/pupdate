using System;
using System.Collections.ObjectModel;
using Pannella.Helpers;
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
    private readonly Label info;
    private readonly Button toggleButton;
    private readonly ListView log;
    private readonly ObservableCollection<string> lines = new();

    /// <summary>Raised when the user clicks the expand/collapse (+/-) button.</summary>
    public event Action ToggleRequested;

    public StatusPane()
    {
        Title = "Status";

        progress = new ProgressBar
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(26),
            Height = 1,
            Fraction = 0f
        };

        // The ProgressBar widget shows neither percentage nor speed, so surface both in a
        // label next to the bar (matching what the old console bar reported).
        info = new Label
        {
            X = Pos.Right(progress) + 1,
            Y = 0,
            Width = 14,
            Height = 1,
            Text = string.Empty
        };

        // Clickable expand/collapse control at the top-right of the pane. Natural sizing
        // (X/Y/Text only) is the only config that renders reliably here — Dim.Auto / a fixed
        // small Width drew an empty block. (Adornments aren't view containers in this Terminal.Gui
        // build, so a control literally on the border line isn't available; top-right is closest.)
        // "+" = expand, "−" = collapse; raises ToggleRequested.
        toggleButton = new Button
        {
            X = Pos.Right(info) + 1,
            Y = 0,
            Text = "+"
        };

        toggleButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            ToggleRequested?.Invoke();
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

        // Show a vertical scrollbar whenever the log overflows the visible area. (The plain
        // .Visible flag isn't enough for ListView; VisibilityMode is the supported toggle.)
        log.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        Add(progress);
        Add(info);
        Add(toggleButton);
        Add(log);
    }

    /// <summary>Updates the +/- button glyph to reflect whether the pane is currently expanded.</summary>
    public void SetExpanded(bool expanded)
    {
        toggleButton.Text = expanded ? "−" : "+";
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

    public void SetProgress(double fraction, double bytesPerSecond)
    {
        double clamped = Math.Clamp(fraction, 0d, 1d);

        progress.Fraction = (float)clamped;
        info.Text = bytesPerSecond > 0
            ? $"{clamped * 100,3:0}%  {ConsoleHelper.FormatSpeed(bytesPerSecond)}"
            : $"{clamped * 100,3:0}%";
    }
}
