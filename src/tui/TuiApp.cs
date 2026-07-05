using System;
using System.Collections.Generic;
using System.Text;
using Pannella.Helpers;
using Pannella.Models.Events;
using Pannella.Services;
using Terminal.Gui.Drawing;

namespace Pannella.TUI;

/// <summary>
/// Entry point and status/progress hub for the full-screen TUI.
///
/// The status and complete sinks are registered with <see cref="ServiceHelper.Initialize"/>
/// (and on the <see cref="CoreUpdaterService"/>) BEFORE the TUI is built, so messages that
/// fire during pre-flight (initialization, missing-core checks) are captured in a buffer and
/// drained into the status pane once it exists. Every UI mutation is marshaled onto the main
/// loop with <see cref="TuiHost.Invoke"/> because long operations run on background tasks.
/// </summary>
public static class TuiApp
{
    private static readonly object SYNC = new();
    private static readonly List<string> preInitBuffer = new();
    private static StatusPane statusPane;
    private static bool running;

    // When set, status messages are routed here instead of the main pane (e.g. a plugin-run modal
    // captures the plugin's output for the duration of the run so it doesn't clog the pane).
    private static Action<string> statusRedirect;

    /// <summary>
    /// Temporarily route status messages somewhere other than the main pane (pass null to restore).
    /// The target is responsible for marshaling onto the UI thread.
    /// </summary>
    public static void SetStatusRedirect(Action<string> redirect)
    {
        lock (SYNC)
        {
            statusRedirect = redirect;
        }
    }

    /// <summary>Status sink wired into ServiceHelper.Initialize so it survives ReloadSettings.</summary>
    public static void StatusSink(object sender, StatusUpdatedEventArgs e) => PostStatus(e.Message);

    /// <summary>Update-process-complete sink; renders the summary into the log pane.</summary>
    public static void CompleteSink(object sender, UpdateProcessCompleteEventArgs e)
    {
        foreach (string line in SummaryLines(e))
        {
            PostStatus(line);
        }
    }

    public static void PostStatus(string message)
    {
        if (message == null)
        {
            return;
        }

        Action<string> redirect;

        lock (SYNC)
        {
            redirect = statusRedirect;

            if (redirect == null && (statusPane == null || !running))
            {
                preInitBuffer.Add(message);
                return;
            }
        }

        if (redirect != null)
        {
            redirect(message);
            return;
        }

        TuiHost.Invoke(() => statusPane.AppendLine(message));
    }

    public static void PostProgress(double fraction, double bytesPerSecond)
    {
        StatusPane pane;

        lock (SYNC)
        {
            if (statusPane == null || !running)
            {
                return;
            }

            pane = statusPane;
        }

        TuiHost.Invoke(() => pane.SetProgress(fraction, bytesPerSecond));
    }

    public static void Run(CoreUpdaterService coreUpdaterService)
    {
        // ConfigurationManager must be enabled before the application instance is created so the
        // library themes are loaded when the driver initializes.
        TuiTheme.EnsureEnabled();
        TuiHost.Init();

        // Apply the saved color theme before the glyph overrides below, so our checkbox glyphs win
        // regardless of the theme's own glyph config.
        TuiTheme.Apply(ServiceHelper.SettingsService.Config.tui_theme);

        // Marking lists render the checked/unchecked state with the CheckState glyphs (default
        // ☑/☐). Swap to a high-contrast filled/empty square pair everywhere. (Selected/UnSelected
        // are set too in case any list uses those instead.)
        Glyphs.CheckStateChecked = new Rune('■');
        Glyphs.CheckStateUnChecked = new Rune('□');
        Glyphs.Selected = new Rune('■');
        Glyphs.UnSelected = new Rune('□');

        // Route plugin prompts to TUI modals (the classic menu leaves these at their Console
        // defaults). Plugin output is captured per-run by PluginRunModal, which overrides
        // OutputHandler while a plugin runs; this is the fallback when none is active.
        PluginService.OutputHandler = message => PostStatus(message?.TrimEnd());
        PluginService.ChoiceHandler = TuiPluginPrompts.Choice;
        PluginService.TextHandler = TuiPluginPrompts.Text;

        EventHandler<DownloadProgressEventArgs> progressHandler = (_, e) => PostProgress(e.Progress, e.BytesPerSecond);
        HttpHelper.Instance.DownloadProgressUpdate += progressHandler;

        try
        {
            var context = new TuiContext(coreUpdaterService);
            var shell = new TuiShell(context);

            lock (SYNC)
            {
                statusPane = shell.StatusPane;
                running = true;

                // Drain anything buffered during pre-flight straight into the pane
                // (we are on the main thread, before the loop starts).
                foreach (string message in preInitBuffer)
                {
                    statusPane.AppendLine(message);
                }

                preInitBuffer.Clear();
            }

            // Show the support/donation message once, on top of the shell. Queued now so it fires
            // as soon as the main loop starts (a modal can't run before the loop exists).
            TuiHost.Invoke(SupportModal.Show);

            TuiHost.Run(shell);
        }
        finally
        {
            lock (SYNC)
            {
                running = false;
                statusPane = null;
            }

            HttpHelper.Instance.DownloadProgressUpdate -= progressHandler;

            // Always restore the terminal, even if the run threw.
            TuiHost.Shutdown();
        }
    }

    private static IEnumerable<string> SummaryLines(UpdateProcessCompleteEventArgs e)
    {
        yield return new string('-', 40);

        if (!string.IsNullOrEmpty(e.Message))
        {
            yield return e.Message;
        }

        if (e.InstalledCores is { Count: > 0 })
        {
            yield return "Cores Updated:";

            foreach (Dictionary<string, string> core in e.InstalledCores)
            {
                yield return $"  {core["core"]} {core["version"]}";
            }
        }

        if (e.InstalledAssets is { Count: > 0 })
        {
            yield return $"Assets Installed: {e.InstalledAssets.Count}";
        }

        if (e.SkippedAssets is { Count: > 0 })
        {
            yield return $"Assets Not Found: {e.SkippedAssets.Count}";
        }

        if (!string.IsNullOrEmpty(e.FirmwareUpdated))
        {
            yield return $"New firmware downloaded ({e.FirmwareUpdated}). Restart your Pocket to install.";
        }

        if (e.MissingLicenses is { Count: > 0 })
        {
            yield return "Missing or incorrect License file for:";

            foreach (string core in e.MissingLicenses)
            {
                yield return $"  {core}";
            }
        }

        if (e.ErrorCount > 0)
        {
            yield return $"{e.ErrorCount} core(s) failed to update. See the log above for details.";
        }
    }
}
