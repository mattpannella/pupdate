namespace Pannella.Helpers;

public static class ConsoleHelper
{
    // Fallback width used when there is no real console attached (headless / output
    // redirected to a pipe or file). In those cases Console.WindowWidth returns 0,
    // which previously produced negative string lengths and crashed the progress bar.
    private const int DEFAULT_WINDOW_WIDTH = 80;

    private static string FormatSpeed(double bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        double value = bytesPerSecond < 0 ? 0 : bytesPerSecond;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Fixed-width field (e.g. " 12.34 MB/s") keeps the progress bar from shifting.
        return $"{value,6:0.00} {units[unit],-4}";
    }

    private static int GetWindowWidth()
    {
        try
        {
            int width = Console.WindowWidth;

            return width > 0 ? width : DEFAULT_WINDOW_WIDTH;
        }
        catch
        {
            // Console.WindowWidth throws on some platforms when no console is attached.
            return DEFAULT_WINDOW_WIDTH;
        }
    }

    public static void ShowProgressBar(long current, long total, double? bytesPerSecond = null)
    {
        if (total <= 0)
        {
            return;
        }

        int windowWidth = GetWindowWidth();
        double progress = Math.Clamp((double)current / total, 0d, 1d);

        // Speed is formatted to a fixed width so the bar doesn't jitter as the rate changes.
        string suffix = bytesPerSecond.HasValue
            ? $"] {progress * 100:0.00}% {FormatSpeed(bytesPerSecond.Value)}"
            : $"] {progress * 100:0.00}%";

        int progressWidth = Math.Max(0, windowWidth - suffix.Length - 2);
        int progressBarWidth = Math.Clamp((int)(progress * progressWidth), 0, progressWidth);

        var progressBar = new string('=', progressBarWidth);
        var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);

        Console.Write($"\r{progressBar}{emptyProgressBar}{suffix}");

        if (current == total)
        {
            // CursorLeft is only meaningful with a real console; skip it when redirected.
            if (!Console.IsOutputRedirected)
            {
                Console.CursorLeft = 0;
                Console.Write(new string(' ', windowWidth));
                Console.CursorLeft = 0;
            }

            Console.Write("\r");
        }
    }
}
