using System.Linq;
using Pannella.Helpers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Setup tab. Phase 2/3: "Manage Display Modes" drives CoresService directly (no CLI-shared
/// EnableDisplayModes), prompting merge/overwrite via a MessageBox and applying to all enabled
/// cores on a background task. More Setup actions (image packs, palettes, Analogizer, etc.) land
/// in later increments.
/// </summary>
public sealed class SetupTab : FrameView
{
    public SetupTab(TuiContext context)
    {
        Title = "Setup";

        var displayModes = new Button
        {
            X = 1,
            Y = 1,
            Text = "Manage _Display Modes"
        };

        displayModes.Accepting += (_, e) =>
        {
            e.Handled = true;

            var values = DisplayModeSelectorDialog.Show(ServiceHelper.CoresService.AllDisplayModes);

            if (values == null)
            {
                TuiApp.PostStatus("Display mode selection cancelled.");
                return;
            }

            // Merge vs overwrite (replaces the classic Console M/O prompt). 0=Merge, 1=Overwrite.
            int choice = MessageBox.Query(App, "Display Modes",
                "Merge the selected display modes with existing ones, or overwrite?",
                "Merge", "Overwrite", "Cancel") ?? 2;

            if (choice == 2)
            {
                TuiApp.PostStatus("Display mode update cancelled.");
                return;
            }

            bool merge = choice == 0;
            var displayModeList = ServiceHelper.CoresService.ConvertDisplayModes(values);
            var coreIds = ServiceHelper.CoresService.Cores
                .Where(core => !ServiceHelper.SettingsService.GetCoreSettings(core.id).skip)
                .Select(core => core.id)
                .ToList();

            context.RunBackground(displayModes, () =>
            {
                foreach (var id in coreIds)
                {
                    TuiApp.PostStatus($"Updating display modes for {id}");
                    ServiceHelper.CoresService.AddDisplayModes(id, displayModeList, isCurated: false, merge: merge);
                }

                ServiceHelper.SettingsService.Save();
                TuiApp.PostStatus($"Display modes updated for {coreIds.Count} core(s).");
            });
        };

        var hint = new Label
        {
            X = 1,
            Y = 3,
            Text = "Apply selected display modes to all enabled cores."
        };

        Add(displayModes);
        Add(hint);
    }
}
