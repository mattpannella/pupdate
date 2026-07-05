using System;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Pannella.TUI;

// The single seam over Terminal.Gui's application loop. Holds the one IApplication instance
// (created via Application.Create()) so no other TUI file needs a handle to it.
internal static class TuiHost
{
    private static IApplication app;

    public static void Init()
    {
        app = Application.Create();
        app.Init();
    }

    public static void Run(IRunnable runnable) => app.Run(runnable);

    public static void Shutdown()
    {
        app?.Dispose();
        app = null;
    }

    /// <summary>Marshals an action onto the main UI loop. Safe to call from a background thread.</summary>
    public static void Invoke(Action action) => app.Invoke(action);

    /// <summary>Requests a clean stop of the running app loop.</summary>
    public static void RequestStop() => app.RequestStop();

    /// <summary>Forces a full layout + redraw of the running UI (e.g. after a live theme change).</summary>
    public static void Refresh() => app.LayoutAndDraw(true);

    /// <summary>Subscribes to the app-wide key stream, which fires before the focused view — so a
    /// handler can pre-empt that view (and any focus) by setting <c>Key.Handled</c>.</summary>
    public static void AddGlobalKeyDown(EventHandler<Key> handler) => app.Keyboard.KeyDown += handler;

    /// <summary>True when <paramref name="view"/> is the top runnable (no modal dialog on top).</summary>
    public static bool IsTopRunnable(View view) => app.TopRunnableView == view;

    /// <summary>Schedules <paramref name="callback"/> on the main loop after <paramref name="time"/>;
    /// it repeats while it returns true. Returns a token for <see cref="RemoveTimeout"/>.</summary>
    public static object AddTimeout(TimeSpan time, Func<bool> callback) => app.AddTimeout(time, callback);

    public static void RemoveTimeout(object token) => app?.RemoveTimeout(token);
}
