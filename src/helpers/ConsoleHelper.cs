namespace Pannella.Helpers;

public static class ConsoleHelper
{
    // Fallback width used when there is no real console attached (headless / output
    // redirected to a pipe or file). In those cases Console.WindowWidth returns 0,
    // which previously produced negative string lengths and crashed the progress bar.
    private const int DEFAULT_WINDOW_WIDTH = 80;

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

    public static void ShowProgressBar(long current, long total)
    {
        if (total <= 0)
        {
            return;
        }

        int windowWidth = GetWindowWidth();
        double progress = Math.Clamp((double)current / total, 0d, 1d);

        int progressWidth = Math.Max(0, windowWidth - 14);
        int progressBarWidth = Math.Clamp((int)(progress * progressWidth), 0, progressWidth);

        var progressBar = new string('=', progressBarWidth);
        var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);

        Console.Write($"\r{progressBar}{emptyProgressBar}] {progress * 100:0.00}%");

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
