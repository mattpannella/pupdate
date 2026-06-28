using System.Collections.Generic;
using Pannella.Helpers;

namespace Pannella.TUI;

/// <summary>
/// Cores tab. "Select Cores" opens the checklist dialog and applies the result via the same
/// SettingsService.EnableCore/DisableCore path the classic menu used.
/// </summary>
public sealed class CoresTab : ActionMenuTab
{
    public CoresTab(TuiContext context) : base(context, "Cores")
    {
        AddAction("Select Cores", SelectCores);
    }

    private void SelectCores()
    {
        var result = CoreSelectorDialog.Show(ServiceHelper.CoresService.Cores,
            "Select the cores you want installed.");

        if (result == null)
        {
            TuiApp.PostStatus("Core selection cancelled.");
            return;
        }

        int enabled = 0;
        int disabled = 0;

        foreach (var kvp in result)
        {
            if (kvp.Value)
            {
                ServiceHelper.SettingsService.EnableCore(kvp.Key);
                enabled++;
            }
            else
            {
                ServiceHelper.SettingsService.DisableCore(kvp.Key);
                disabled++;
            }
        }

        ServiceHelper.SettingsService.Save();

        // Reload BOTH so a subsequent Update uses the new selection (issue #299); re-attaches
        // the TUI status sink to the rebuilt services.
        ServiceHelper.ReloadSettings();
        Context.CoreUpdater.ReloadSettings();

        TuiApp.PostStatus($"Core selection saved: {enabled} enabled, {disabled} disabled.");
    }
}
