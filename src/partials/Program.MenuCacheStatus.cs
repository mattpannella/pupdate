using Pannella.Helpers;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    // Colors only when the console isn't redirected; otherwise plain text (no leaked escape codes).
    // Matches how AnalogizerSettingsService gates its ANSI output on this branch.
    private static readonly bool MenuCacheColorEnabled = !Console.IsOutputRedirected;

    private const string AnsiReset = "\x1b[0m";
    private const string AnsiGreen = "\x1b[92m";
    private const string AnsiYellow = "\x1b[93m";
    private const string AnsiRed = "\x1b[91m";

    private const string MenuCacheLabel = "openFPGA platform limit";

    private static string MenuCacheColor(MenuCacheService.MenuCacheLevel level)
    {
        if (!MenuCacheColorEnabled)
        {
            return string.Empty;
        }

        return level switch
        {
            MenuCacheService.MenuCacheLevel.Safe => AnsiGreen,
            MenuCacheService.MenuCacheLevel.Close => AnsiYellow,
            _ => AnsiRed
        };
    }

    // Null on any failure so the indicator just doesn't render rather than breaking the menu.
    internal static MenuCacheService.MenuCacheStatus GetMenuCacheStatus()
    {
        try
        {
            return new MenuCacheService(ServiceHelper.UpdateDirectory, ServiceHelper.CoresService).GetStatus();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildMenuCacheMeter(MenuCacheService.MenuCacheStatus status, int width = 18)
    {
        if (status == null)
        {
            return string.Empty;
        }

        string color = MenuCacheColor(status.Level);
        string reset = MenuCacheColorEnabled ? AnsiReset : string.Empty;

        if (status.IsOverLimit)
        {
            int overBy = Math.Max(1, (int)Math.Ceiling(-status.RemainingBytes / (double)MenuCacheService.ApproxPlatformCost));

            return $"{color}{MenuCacheLabel}  EXCEEDED - the Pocket will crash when loading openFPGA; archive about " +
                   $"{overBy} platform{(overBy == 1 ? "" : "s")} to recover.{reset}";
        }

        double fraction = Math.Clamp(status.Fraction, 0, 1);
        int filled = (int)Math.Round(fraction * width);
        string bar = new string('#', filled) + new string('-', width - filled);
        int percent = (int)Math.Round(fraction * 100);
        string headroom = $"~{status.RemainingPlatforms} platform{(status.RemainingPlatforms == 1 ? "" : "s")} left";

        return $"{color}{MenuCacheLabel}  [{bar}]  {percent,3}%   {headroom}{reset}";
    }

    private static string GetMenuCacheStatusLine()
    {
        if (!ServiceHelper.SettingsService.Config.show_menu_cache_status)
        {
            return string.Empty;
        }

        return BuildMenuCacheMeter(GetMenuCacheStatus());
    }

    private static void WarnIfPlatformLimitExceeded()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        MenuCacheService.MenuCacheStatus status = GetMenuCacheStatus();

        if (status is not { IsOverLimit: true })
        {
            return;
        }

        int overBy = Math.Max(1, (int)Math.Ceiling(-status.RemainingBytes / (double)MenuCacheService.ApproxPlatformCost));
        string color = MenuCacheColorEnabled ? AnsiRed : string.Empty;
        string reset = MenuCacheColorEnabled ? AnsiReset : string.Empty;

        Console.WriteLine();
        Console.WriteLine($"{color}======================================================================{reset}");
        Console.WriteLine($"{color} WARNING: you are over the openFPGA platform limit.{reset}");
        Console.WriteLine($"{color}{reset}");
        Console.WriteLine($"{color} You have {status.InstalledPlatforms} installed platforms - more than the Analogue Pocket can{reset}");
        Console.WriteLine($"{color} build its openFPGA menu for. Until you archive about {overBy} platform{(overBy == 1 ? "" : "s")}, the{reset}");
        Console.WriteLine($"{color} Pocket will crash to a QR code when it rebuilds the menu on boot.{reset}");
        Console.WriteLine($"{color}{reset}");
        Console.WriteLine($"{color} Fix it under:  Pocket Maintenance  >  Archive/Unarchive Platforms{reset}");
        Console.WriteLine($"{color}======================================================================{reset}");
        Console.WriteLine();
        Pause();
    }
}
