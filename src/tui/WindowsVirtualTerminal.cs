using System.Runtime.InteropServices;

namespace Pannella.TUI;

/// <summary>
/// Turns on ANSI/VT processing for the Windows console before the TUI starts. Windows Terminal
/// enables it by default, but the legacy console host (conhost) does not — e.g. double-clicking
/// pupdate.exe when conhost is the default terminal — and Terminal.Gui's windows driver then
/// falls into its "legacy console" mode, rendering a blank but responsive screen (issue #490).
/// </summary>
internal static class WindowsVirtualTerminal
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    private static IntPtr outputHandle;
    private static uint originalMode;
    private static bool modeChanged;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    /// <summary>
    /// True when the console can process ANSI sequences (it already could, or it was just
    /// enabled). Always true off-Windows. False means the TUI would render blank here.
    /// </summary>
    public static bool TryEnable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            outputHandle = GetStdHandle(STD_OUTPUT_HANDLE);

            if (outputHandle == IntPtr.Zero || outputHandle == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            if (!GetConsoleMode(outputHandle, out uint mode))
            {
                return false;
            }

            if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0)
            {
                return true;
            }

            if (!SetConsoleMode(outputHandle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING))
            {
                return false;
            }

            originalMode = mode;
            modeChanged = true;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Puts the console mode back if <see cref="TryEnable"/> changed it. Must run after the TUI
    /// has fully shut down — Terminal.Gui's terminal-restore sequences need VT still enabled.
    /// </summary>
    public static void Restore()
    {
        if (!modeChanged)
        {
            return;
        }

        try
        {
            SetConsoleMode(outputHandle, originalMode);
        }
        catch
        {
            // Best effort; the console closes with the process anyway.
        }

        modeChanged = false;
    }
}
