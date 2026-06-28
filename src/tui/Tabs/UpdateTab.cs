using Pannella.Helpers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Phase 1 tab: proves the end-to-end status/progress/threading flow by wiring
/// "Update All" and "Update Firmware" to the existing services via TuiContext.RunBackground.
/// </summary>
public sealed class UpdateTab : FrameView
{
    public UpdateTab(TuiContext context)
    {
        Title = "Update";

        var updateAll = new Button
        {
            X = 1,
            Y = 1,
            Text = "Update _All"
        };

        updateAll.Accepting += (_, e) =>
        {
            e.Handled = true;
            context.RunBackground(updateAll, () =>
            {
                TuiApp.PostStatus("Starting update process...");
                int errors = context.CoreUpdater.RunUpdates(null, false, false);
                TuiApp.PostStatus(errors > 0
                    ? $"Update finished with {errors} error(s)."
                    : "Update complete.");
            });
        };

        var updateFirmware = new Button
        {
            X = 1,
            Y = 3,
            Text = "Update _Firmware"
        };

        updateFirmware.Accepting += (_, e) =>
        {
            e.Handled = true;
            context.RunBackground(updateFirmware, () =>
            {
                ServiceHelper.FirmwareService.UpdateFirmware(ServiceHelper.UpdateDirectory);
                TuiApp.PostStatus("Firmware check complete.");
            });
        };

        var hint = new Label
        {
            X = 1,
            Y = 5,
            Text = "Output streams to the Status pane below. Use Tab/arrows to navigate, Esc or Quit to exit."
        };

        Add(updateAll);
        Add(updateFirmware);
        Add(hint);
    }
}
