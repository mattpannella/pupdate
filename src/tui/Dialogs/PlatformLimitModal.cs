using Pannella.Services;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;
public static class PlatformLimitModal
{
    public static void Show()
    {
        MenuCacheService.MenuCacheStatus status = Program.GetMenuCacheStatus();

        if (status is not { IsOverLimit: true })
        {
            return;
        }

        int overBy = System.Math.Max(1,
            (int)System.Math.Ceiling(-status.RemainingBytes / (double)MenuCacheService.ApproxPlatformCost));

        string message =
            $"You have {status.InstalledPlatforms} installed platforms - more than the Analogue Pocket can\n" +
            "build its openFPGA menu for.\n\n" +
            $"Until you archive about {overBy} platform{(overBy == 1 ? "" : "s")}, the Pocket will crash to a\n" +
            "QR code when it rebuilds the menu on boot.\n\n" +
            "Fix it under:  Maintenance  >  Archive Unused Platforms";

        var dialog = new Dialog
        {
            Title = "openFPGA platform limit exceeded",
            Width = Dim.Percent(70),
            Height = Dim.Percent(45)
        };

        dialog.Add(new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            Text = message,
            CanFocus = false
        });

        var ok = new Button { Text = "_Continue" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;
            TuiHost.RequestStop();
        };

        dialog.AddButton(ok);
        TuiHost.Run(dialog);
    }
}
