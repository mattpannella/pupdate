namespace Pannella.Helpers;

public class ConsoleHelper
{
    // public static void ShowProgressBar(int current, int total)
    // {
    //     var progress = (double)current / total;
    //     var progressWidth = Console.WindowWidth - 14;
    //     var progressBarWidth = (int)(progress * progressWidth);
    //     var progressBar = new string('=', progressBarWidth);
    //     var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);
    //
    //     Console.Write($"\r{progressBar}{emptyProgressBar}] {(progress * 100):0.00}%");
    //
    //     if (current == total)
    //     {
    //         Console.CursorLeft = 0;
    //         Console.Write(new string(' ', Console.WindowWidth));
    //         Console.CursorLeft = 0;
    //         Console.Write("\r");
    //     }
    // }

    public static void ShowProgressBar(long current, long total)
    {
        var progress = (double)current / total;
        var progressWidth = Console.WindowWidth - 14;
        var progressBarWidth = (int)(progress * progressWidth);
        var progressBar = new string('=', progressBarWidth);
        var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);

        Console.Write($"\r{progressBar}{emptyProgressBar}] {(progress * 100):0.00}%");

        if (current == total)
        {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.WindowWidth));
            Console.CursorLeft = 0;
            Console.Write("\r");
        }
    }
}
