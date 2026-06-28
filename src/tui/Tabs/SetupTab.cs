using System;
using System.Linq;
using Pannella.Helpers;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Setup tab. "Manage Display Modes" drives CoresService directly (no CLI-shared
/// EnableDisplayModes), prompting merge/overwrite and applying to all enabled cores on a
/// background task. More Setup actions (image packs, palettes, tokens, Analogizer, etc.) land in
/// later increments.
/// </summary>
public sealed class SetupTab : ActionMenuTab
{
    public SetupTab(TuiContext context) : base(context, "Setup")
    {
        AddAction("Manage Display Modes", ManageDisplayModes);
        AddAction("Set GitHub Token", SetGitHubToken);
    }

    private void SetGitHubToken()
    {
        var config = ServiceHelper.SettingsService.Config;

        string input = TuiPrompts.PromptText("GitHub Token",
            "Enter your GitHub personal access token (leave blank to clear):",
            config.github_token ?? string.Empty, secret: true);

        if (input == null)
        {
            TuiApp.PostStatus("GitHub token unchanged.");
            return;
        }

        string newToken = string.IsNullOrWhiteSpace(input) ? string.Empty : input.Trim();

        if (string.Equals(config.github_token, newToken, StringComparison.Ordinal))
        {
            TuiApp.PostStatus("GitHub token unchanged.");
            return;
        }

        config.github_token = newToken;
        ServiceHelper.SettingsService.Save();
        ServiceHelper.ReloadSettings();
        Context.CoreUpdater.ReloadSettings();

        TuiApp.PostStatus("GitHub token updated.");
    }

    private void ManageDisplayModes()
    {
        var values = DisplayModeSelectorDialog.Show(ServiceHelper.CoresService.AllDisplayModes);

        if (values == null)
        {
            TuiApp.PostStatus("Display mode selection cancelled.");
            return;
        }

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

        Context.RunBackground(null, () =>
        {
            foreach (var id in coreIds)
            {
                TuiApp.PostStatus($"Updating display modes for {id}");
                ServiceHelper.CoresService.AddDisplayModes(id, displayModeList, isCurated: false, merge: merge);
            }

            ServiceHelper.SettingsService.Save();
            TuiApp.PostStatus($"Display modes updated for {coreIds.Count} core(s).");
        });
    }
}
