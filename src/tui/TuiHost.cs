using System;
using Terminal.Gui.App;

namespace Pannella.TUI;

// The single seam over Terminal.Gui's static Application API.
//
// That static API is marked obsolete in v2.4.12 ("the legacy static Application object is going
// away"), but its replacement, ApplicationImpl, is internal — so the static API is currently the
// only public way to bootstrap and drive the app loop. Isolating every reference behind this one
// class keeps the deprecation suppression to a single file (rather than a pragma in every TUI
// file) and makes the eventual move to the instance-based IApplication a one-file change.
#pragma warning disable CS0618

internal static class TuiHost
{
    public static void Init() => Application.Init();

    public static void Run(IRunnable runnable) => Application.Run(runnable);

    public static void Shutdown() => Application.Shutdown();

    /// <summary>Marshals an action onto the main UI loop. Safe to call from a background thread.</summary>
    public static void Invoke(Action action) => Application.Invoke(action);

    /// <summary>Requests a clean stop of the running app loop.</summary>
    public static void RequestStop() => Application.RequestStop();

    /// <summary>Forces a full layout + redraw of the running UI (e.g. after a live theme change).</summary>
    public static void Refresh() => Application.LayoutAndDraw(true);
}
