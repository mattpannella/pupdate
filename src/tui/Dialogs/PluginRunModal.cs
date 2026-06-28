using System;
using System.Threading.Tasks;
using Pannella.Services;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Runs a plugin inside a dedicated modal so its (often chatty, sometimes progress-bar) output
/// streams here instead of clogging the main Status pane. While the plugin runs, both its raw
/// output (print_msg, honoring \r/\n via <see cref="LogView"/>) and its status messages are routed
/// into this modal's log; choice/text prompts still pop as nested modals on top. The plugin runs on
/// a background task; the modal stays open after it finishes so the user can review, then Close
/// dismisses it and normal routing is restored.
/// </summary>
public static class PluginRunModal
{
    public static void Run(string pluginName, Action work)
    {
        var dialog = new Dialog
        {
            Title = $"Plugin: {pluginName}",
            Width = Dim.Percent(85),
            Height = Dim.Percent(85)
        };

        var log = new LogView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        var close = new Button { Text = "_Close", Enabled = false };
        close.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(close);
        dialog.Add(log);

        // Capture plugin output + status into this modal for the duration of the run.
        var previousOutput = PluginService.OutputHandler;
        PluginService.OutputHandler = text => TuiHost.Invoke(() => log.WriteRaw(text));
        TuiApp.SetStatusRedirect(message => TuiHost.Invoke(() => log.AppendLine(message)));

        Task.Run(() =>
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                TuiHost.Invoke(() => log.AppendLine($"Error: {ex.Message}"));
            }
            finally
            {
                TuiHost.Invoke(() =>
                {
                    log.AppendLine(string.Empty);
                    log.AppendLine("— finished — press Close —");
                    close.Enabled = true;
                    close.SetFocus();
                });
            }
        });

        TuiHost.Run(dialog);

        // Restore normal routing (the run is finished by the time Close becomes enabled).
        PluginService.OutputHandler = previousOutput;
        TuiApp.SetStatusRedirect(null);
    }
}
