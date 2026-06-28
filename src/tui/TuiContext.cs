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

    private readonly object busyLock = new();
    private bool busy;

    /// <summary>True while a background operation is in flight.</summary>
    public bool IsBusy
    {
        get { lock (busyLock) { return busy; } }
    }

    /// <summary>Raised on the UI thread when an operation starts (true) or finishes (false).</summary>
    public event Action<bool> BusyChanged;

    /// <summary>
    /// Runs <paramref name="work"/> on a background task, disabling <paramref name="trigger"/>
    /// for the duration so it can't be re-entered. Exceptions are surfaced to the status pane
    /// rather than crashing the UI thread. The button is re-enabled on the main loop via
    /// <see cref="TuiHost.Invoke"/>.
    /// </summary>
    public void RunBackground(Button trigger, Action work)
    {
        // Only one long operation at a time: the underlying services aren't safe to run
        // concurrently, so reject a second action (from any button) while one is in flight.
        lock (busyLock)
        {
            if (busy)
            {
                TuiApp.PostStatus("An operation is already running — please wait for it to finish.");
                return;
            }

            busy = true;
        }

        // RunBackground is invoked from a button handler (UI thread), so this is safe to raise
        // directly; the shell expands the status pane while work runs.
        BusyChanged?.Invoke(true);

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
                lock (busyLock)
                {
                    busy = false;
                }

                TuiHost.Invoke(() =>
                {
                    if (trigger != null)
                    {
                        trigger.Enabled = true;
                    }

                    BusyChanged?.Invoke(false);
                });
            }
        });
    }
}
