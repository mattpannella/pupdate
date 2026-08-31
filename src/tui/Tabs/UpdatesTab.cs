using System.Linq;
using Pannella.Helpers;

namespace Pannella.TUI;

/// <summary>
/// Updates tab: the primary day-to-day actions - full update, asset download, and firmware check.
/// Choosing which cores pupdate manages lives on the dedicated <see cref="CoresTab"/>.
/// </summary>
public sealed class UpdatesTab : ActionMenuTab
{
    public UpdatesTab(TuiContext context) : base(context, "Updates")
    {
        AddAction("Update All", () =>
            Context.RunBackground(null, () =>
            {
                TuiApp.PostStatus("Starting update process...");
                int errors = Context.CoreUpdater.RunUpdates(null, clean: false, onlyUpdatedAssets: false);
                TuiApp.PostStatus(errors > 0 ? $"Update finished with {errors} error(s)." : "Update complete.");
            }));

        AddAction("Download Assets", () =>
            Context.RunBackground(null, () =>
            {
                TuiApp.PostStatus("Checking for required files...");
                var cores = ServiceHelper.CoresService.Cores
                    .Where(core => !ServiceHelper.SettingsService.GetCoreSettings(core.id).skip)
                    .ToList();
                ServiceHelper.CoresService.DownloadCoreAssets(cores);
                TuiApp.PostStatus("Asset check complete.");
            }));

        AddAction("Update Firmware", () =>
            Context.RunBackground(null, () =>
            {
                ServiceHelper.FirmwareService.UpdateFirmware(ServiceHelper.UpdateDirectory);
                TuiApp.PostStatus("Firmware check complete.");
            }));
    }
}
