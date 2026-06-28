using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Main tab: the primary day-to-day actions — full update, asset download, firmware check, and
/// choosing which cores pupdate manages. (Merges the former Update + Cores tabs.)
/// </summary>
public sealed class MainTab : ActionMenuTab
{
    public MainTab(TuiContext context) : base(context, "Main")
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

        AddAction("Select Cores", SelectCores);
    }

    private void SelectCores()
    {
        var settings = ServiceHelper.SettingsService;
        var config = settings.Config;

        // Mirror AskAboutNewCores(force:true): set the default new-core install mode first.
        int mode = MessageBox.Query(App, "New Cores",
            "Would you like to, by default, install new cores?",
            "Yes", "No", "Ask for each", "Cancel") ?? 3;

        if (mode == 3)
        {
            TuiApp.PostStatus("Core selection cancelled.");
            return;
        }

        config.download_new_cores = mode switch
        {
            0 => "yes",
            1 => "no",
            _ => "ask"
        };

        // "yes" enables every core without showing the list (matches RunCoreSelector).
        if (config.download_new_cores == "yes")
        {
            foreach (var core in ServiceHelper.CoresService.Cores)
            {
                settings.EnableCore(core.id);
            }

            Apply($"All {ServiceHelper.CoresService.Cores.Count} cores enabled.");
            return;
        }

        // No / Ask → present the checklist.
        var result = CoreSelectorDialog.Show(ServiceHelper.CoresService.Cores,
            "Select the cores you want installed.");

        if (result == null)
        {
            Apply("Install mode saved (core list unchanged).");
            return;
        }

        int enabled = 0;
        int disabled = 0;

        foreach (var kvp in result)
        {
            if (kvp.Value)
            {
                settings.EnableCore(kvp.Key);
                enabled++;
            }
            else
            {
                settings.DisableCore(kvp.Key);
                disabled++;
            }
        }

        Apply($"Core selection saved: {enabled} enabled, {disabled} disabled.");
    }

    private void Apply(string message)
    {
        ServiceHelper.SettingsService.Save();
        ServiceHelper.ReloadSettings();
        Context.CoreUpdater.ReloadSettings();
        TuiApp.PostStatus(message);
    }
}
