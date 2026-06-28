using System.Collections.Generic;
using Pannella.Helpers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Cores tab. Phase 2 wires "Select Cores" to the new <see cref="CoreSelectorDialog"/>, applying
/// the result via the same SettingsService.EnableCore/DisableCore path the classic menu used.
/// </summary>
public sealed class CoresTab : FrameView
{
    public CoresTab(TuiContext context)
    {
        Title = "Cores";

        var selectCores = new Button
        {
            X = 1,
            Y = 1,
            Text = "Select _Cores"
        };

        selectCores.Accepting += (_, e) =>
        {
            e.Handled = true;

            // The selector is an interactive modal dialog, so it runs on the UI thread (not
            // RunBackground). Applying the result is a quick settings write + reload.
            var cores = ServiceHelper.CoresService.Cores;
            var result = CoreSelectorDialog.Show(cores, "Select the cores you want installed.");

            if (result == null)
            {
                TuiApp.PostStatus("Core selection cancelled.");
                return;
            }

            ApplySelection(context, result);
        };

        var hint = new Label
        {
            X = 1,
            Y = 3,
            Text = "Choose which cores to keep. Changes take effect on the next Update."
        };

        Add(selectCores);
        Add(hint);
    }

    private static void ApplySelection(TuiContext context, Dictionary<string, bool> result)
    {
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

        // Mirror the classic menu: reload BOTH so a subsequent Update uses the new selection
        // (issue #299). ReloadSettings re-attaches the TUI status sink to the new services.
        ServiceHelper.ReloadSettings();
        context.CoreUpdater.ReloadSettings();

        TuiApp.PostStatus($"Core selection saved: {enabled} enabled, {disabled} disabled.");
    }
}
