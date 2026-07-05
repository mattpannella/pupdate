using System;
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
    private readonly SpinnerView spinner;
    private readonly ProgressBar progress;
    private readonly Label info;
    private readonly Button toggleButton;
    private readonly LogView log;
    private object marqueeToken;

    /// <summary>Raised when the user clicks the expand/collapse (+/-) button.</summary>
    public event Action ToggleRequested;

    public StatusPane()
    {
        Title = "Status";

        // Spins for the whole operation, covering phases with no download progress flowing.
        spinner = new SpinnerView
        {
            X = 0,
            Y = 0,
            Style = new SpinnerStyle.Dots(),
            Visible = false
        };

        progress = new ProgressBar
        {
            X = Pos.Right(spinner) + 1,
            Y = 0,
            Width = Dim.Fill(26),
            Height = 1,
            Fraction = 0f,
            ProgressBarStyle = ProgressBarStyle.Continuous
        };

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

        // Reusable scrollable log (auto-scrollbar, bounded history, \r/\n handling).
        log = new LogView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Add(spinner);
        Add(progress);
        Add(info);
        Add(toggleButton);
        Add(log);
    }

    /// <summary>
    /// Shows/hides the busy indicators. While busy (and until real download progress arrives) the
    /// bar runs as a marquee so long non-download phases don't look frozen.
    /// </summary>
    public void SetBusy(bool busy)
    {
        spinner.Visible = busy;
        spinner.AutoSpin = busy;

        if (busy)
        {
            info.Text = string.Empty;
            StartMarquee();
        }
        else
        {
            StopMarquee();
        }
    }

    private void StartMarquee()
    {
        if (marqueeToken != null)
        {
            return;
        }

        progress.ProgressBarStyle = ProgressBarStyle.MarqueeContinuous;
        progress.Fraction = 0f;

        marqueeToken = TuiHost.AddTimeout(TimeSpan.FromMilliseconds(120), () =>
        {
            if (marqueeToken == null)
            {
                return false;
            }

            progress.Pulse();
            return true;
        });
    }

    private void StopMarquee()
    {
        if (marqueeToken == null)
        {
            return;
        }

        object token = marqueeToken;
        marqueeToken = null;
        TuiHost.RemoveTimeout(token);

        progress.ProgressBarStyle = ProgressBarStyle.Continuous;
        progress.Fraction = 0f;
    }

    /// <summary>Updates the +/- button glyph to reflect whether the pane is currently expanded.</summary>
    public void SetExpanded(bool expanded)
    {
        toggleButton.Text = expanded ? "−" : "+";
    }

    public void AppendLine(string message) => log.AppendLine(message);

    public void SetProgress(double fraction, double bytesPerSecond)
    {
        double clamped = Math.Clamp(fraction, 0d, 1d);

        StopMarquee();
        progress.Fraction = (float)clamped;

        // While downloading, show "% + speed". Once complete the speed is meaningless, so drop it
        // (otherwise the last reading sits frozen next to a full bar after the transfer ends).
        info.Text = bytesPerSecond > 0 && clamped < 1d
            ? $"{clamped * 100,3:0}%  {ConsoleHelper.FormatSpeed(bytesPerSecond)}"
            : $"{clamped * 100,3:0}%";
    }
}
