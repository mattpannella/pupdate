using System.Collections.Generic;
using System.Diagnostics;

namespace Pannella.TUI;

/// <summary>
/// Relaunches pupdate inside Windows Terminal when the full-screen TUI is wanted but we're running
/// under the legacy Windows console host (conhost). Terminal.Gui's modern Windows driver renders a
/// blank (but responsive) screen under conhost
/// </summary>
internal static class WindowsTerminalRelauncher
{
    /// <summary>
    /// Starts pupdate in a new Windows Terminal window and returns true if it did
    /// </summary>
    public static bool TryRelaunch(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // already inside Windows Terminal
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")))
        {
            return false;
        }

        // Explicit loop guard / opt-out (set on the child below; also usable by hand).
        if (string.Equals(Environment.GetEnvironmentVariable("PUPDATE_NO_WT_RELAUNCH"), "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        // piped/automation runs that opted into the TUI: don't yank them into a new window.
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return false;
        }

        string self = Environment.ProcessPath;

        if (string.IsNullOrEmpty(self))
        {
            return false;
        }

        // wt.exe command line: `wt.exe "<self>" <original args...>` — opens a new tab that runs us
        // again in a console Terminal.Gui can draw in. Note: wt.exe treats ';' in its command line
        // as a command separator, but Windows paths can't contain ';' and pupdate's args don't, so
        // forwarding them verbatim is safe.
        var argList = new List<string> { self };
        argList.AddRange(args);

        try
        {
            var psi = new ProcessStartInfo { FileName = "wt.exe", UseShellExecute = false };

            foreach (string arg in argList)
            {
                psi.ArgumentList.Add(arg);
            }

            psi.Environment["PUPDATE_TUI"] = "1";            // child definitely launches the TUI
            psi.Environment["PUPDATE_NO_WT_RELAUNCH"] = "1"; // child never relaunches again

            if (Process.Start(psi) != null)
            {
                return true;
            }
        }
        catch
        {
            // wt.exe is an app-execution alias and may not launch via CreateProcess on some
            // setups; fall through to the shell path below.
        }

        try
        {
            var psi = new ProcessStartInfo { FileName = "wt.exe", UseShellExecute = true };

            foreach (string arg in argList)
            {
                psi.ArgumentList.Add(arg);
            }

            return Process.Start(psi) != null;
        }
        catch
        {
            // Windows Terminal isn't installed / the alias is disabled. nothing to relaunch into.
            return false;
        }
    }
}
