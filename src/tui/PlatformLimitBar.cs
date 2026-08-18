using System;
using Pannella.Helpers;
using Pannella.Services;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Pannella.TUI;

// Persistent openFPGA platform limit gauge pinned at the top of the shell. Call Refresh() after any
// operation that changes what's installed.
public sealed class PlatformLimitBar : View
{
    private readonly Label title;
    private readonly ProgressBar bar;
    private readonly Label detail;

    // Content sits on the middle row (Y=1) of a 3-row strip, leaving a blank row above and below for
    // breathing room. Collapses to 0 rows when hidden so it reserves no space.
    private const int ExpandedHeight = 3;

    public PlatformLimitBar()
    {
        Width = Dim.Fill();
        Height = ExpandedHeight;
        CanFocus = false;

        title = new Label { X = 0, Y = 1, Text = "openFPGA platform limit", CanFocus = false };
        bar = new ProgressBar
        {
            X = Pos.Right(title) + 1,
            Y = 1,
            Width = 24,
            Height = 1,
            Fraction = 0f,
            ProgressBarStyle = ProgressBarStyle.Continuous
        };
        detail = new Label { X = Pos.Right(bar) + 1, Y = 1, Width = Dim.Fill(), Text = string.Empty, CanFocus = false };

        Add(title);
        Add(bar);
        Add(detail);
    }

    public void Refresh()
    {
        MenuCacheService.MenuCacheStatus status = Program.GetMenuCacheStatus();
        bool show = status != null && ServiceHelper.SettingsService.Config.show_menu_cache_status;

        Visible = show;
        Height = show ? ExpandedHeight : 0;
        SuperView?.SetNeedsLayout();

        if (!show)
        {
            return;
        }

        bar.Fraction = (float)Math.Clamp(status.Fraction, 0, 1);

        if (status.IsOverLimit)
        {
            int overBy = Math.Max(1, (int)Math.Ceiling(-status.RemainingBytes / (double)MenuCacheService.ApproxPlatformCost));
            detail.Text = $"EXCEEDED - archive ~{overBy} platform{(overBy == 1 ? "" : "s")} (Pocket will crash on boot)";
        }
        else
        {
            int percent = (int)Math.Round(Math.Clamp(status.Fraction, 0, 1) * 100);
            detail.Text = $"{percent}%  ~{status.RemainingPlatforms} platform{(status.RemainingPlatforms == 1 ? "" : "s")} left";
        }

        ApplyColor(status.Level);
    }

    private void ApplyColor(MenuCacheService.MenuCacheLevel level)
    {
        ColorName16 fg = level switch
        {
            MenuCacheService.MenuCacheLevel.Safe => ColorName16.BrightGreen,
            MenuCacheService.MenuCacheLevel.Close => ColorName16.BrightYellow,
            _ => ColorName16.BrightRed
        };

        try
        {
            Color bg = GetScheme().Normal.Background;
            var scheme = new Scheme { Normal = new Attribute(fg, bg), Focus = new Attribute(fg, bg) };

            SetScheme(scheme);
            title.SetScheme(scheme);
            bar.SetScheme(scheme);
            detail.SetScheme(scheme);
        }
        catch
        {
            // coloring is cosmetic; never let a scheme edge case take down the shell
        }
    }
}
