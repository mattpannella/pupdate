namespace Pannella.Helpers;

public static class ConsoleHelper
{
    public static void ShowProgressBar(long current, long total)
    {
        if (total <= 0) return;
        var progressWidth = Console.WindowWidth - 14;
        if (progressWidth <= 0) return;
        var progress = (double)current / total;
        var progressBarWidth = Math.Clamp((int)(progress * progressWidth), 0, progressWidth);
        var progressBar = new string('=', progressBarWidth);
        var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);

        Console.Write($"\r{progressBar}{emptyProgressBar}] {progress * 100:0.00}%");

        if (current == total)
        {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.WindowWidth));
            Console.CursorLeft = 0;
            Console.Write("\r");
        }
    }
}
