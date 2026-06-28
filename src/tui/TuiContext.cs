using System;
using System.Threading.Tasks;
using Pannella.Helpers;
using Pannella.Services;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Shared state handed to every tab/dialog: the orchestrator plus the helper that
/// runs blocking service calls off the UI thread (so the TUI never freezes) and
/// marshals completion back onto the main loop via <see cref="Application.Invoke(Action)"/>.
/// </summary>
public sealed class TuiContext
{
    public CoreUpdaterService CoreUpdater { get; }

    public TuiContext(CoreUpdaterService coreUpdater)
    {
        CoreUpdater = coreUpdater;
    }

    /// <summary>
    /// Runs <paramref name="work"/> on a background task, disabling <paramref name="trigger"/>
    /// for the duration so it can't be re-entered. Exceptions are surfaced to the status pane
    /// rather than crashing the UI thread. The button is re-enabled on the main loop via
    /// <see cref="TuiHost.Invoke"/>.
    /// </summary>
    public void RunBackground(Button trigger, Action work)
    {
        if (trigger != null)
        {
            trigger.Enabled = false;
        }

        Task.Run(() =>
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                TuiApp.PostStatus($"Error: {Util.GetExceptionMessage(ex)}");
            }
            finally
            {
                TuiHost.Invoke(() =>
                {
                    if (trigger != null)
                    {
                        trigger.Enabled = true;
                    }
                });
            }
        });
    }
}
