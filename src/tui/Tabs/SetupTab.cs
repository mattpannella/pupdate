using System;
using System.Linq;
using Pannella.Helpers;
using Pannella.Services;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Setup tab: display modes, directory locations, GitHub token, and Patreon config. Built entirely
/// from the shared components (ActionMenuTab + ChecklistDialog + TuiPrompts). Image packs/palettes,
/// instance generation, aspect ratio and Analogizer land in later batches.
/// </summary>
public sealed class SetupTab : ActionMenuTab
{
    public SetupTab(TuiContext context) : base(context, "Setup")
    {
        AddAction("Manage Display Modes", ManageDisplayModes);
        AddAction("Set GitHub Token", SetGitHubToken);
        AddAction("Set Backup Saves Location", SetBackupLocation);
        AddAction("Set Archive Cache Location", SetArchiveCacheLocation);
        AddAction("Set Temp Directory", SetTempDirectory);
        AddAction("Set Patreon Session Cookie", SetPatreonCookie);
        AddAction("Test Patreon Session Cookie", TestPatreonCookie);
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

    private void SetBackupLocation()
    {
        var config = ServiceHelper.SettingsService.Config;

        SetDirectory("Backup Saves Location", "Folder for save/memory backups (blank = \"Backups\"):",
            config.backup_saves_location,
            input => string.IsNullOrWhiteSpace(input) ? "Backups" : input.Trim(),
            value => config.backup_saves_location = value,
            reload: false);
    }

    private void SetArchiveCacheLocation()
    {
        var config = ServiceHelper.SettingsService.Config;

        SetDirectory("Archive Cache Location", "Folder for cached archive files (blank = default):",
            config.archive_cache_location,
            input => string.IsNullOrWhiteSpace(input) ? null : input.Trim(),
            value => config.archive_cache_location = value,
            reload: true);
    }

    private void SetTempDirectory()
    {
        var config = ServiceHelper.SettingsService.Config;

        SetDirectory("Temp Directory", "Temp directory (blank = system default):",
            config.temp_directory,
            input => string.IsNullOrWhiteSpace(input) ? null : input.Trim(),
            value => config.temp_directory = value,
            reload: true);
    }

    // Shared directory-setter: prompt, normalize, apply, save, optionally reload services.
    private void SetDirectory(string title, string prompt, string current,
        Func<string, string> normalize, Action<string> apply, bool reload)
    {
        string input = TuiPrompts.PromptText(title, prompt, current ?? string.Empty);

        if (input == null)
        {
            TuiApp.PostStatus($"{title} unchanged.");
            return;
        }

        apply(normalize(input));
        ServiceHelper.SettingsService.Save();

        if (reload)
        {
            ServiceHelper.ReloadSettings();
            Context.CoreUpdater.ReloadSettings();
        }

        TuiApp.PostStatus($"{title} updated.");
    }

    private void SetPatreonCookie()
    {
        var config = ServiceHelper.SettingsService.Config;

        TuiApp.PostStatus("Patreon cookie: in your browser, log in to patreon.com, open DevTools → " +
                          "Application/Storage → Cookies → patreon.com, and copy the 'session_id' value.");

        string input = TuiPrompts.PromptText("Patreon Session Cookie",
            "Paste the patreon.com 'session_id' value (blank to clear):",
            config.patreon_session_cookie ?? string.Empty, secret: true);

        if (input == null)
        {
            TuiApp.PostStatus("Patreon session cookie unchanged.");
            return;
        }

        config.patreon_session_cookie = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        if (!config.jt_beta_patreon_fetch && config.patreon_session_cookie != null
            && TuiPrompts.Confirm(App, "JT Beta", "Enable JT Beta auto-fetch via Patreon?"))
        {
            config.jt_beta_patreon_fetch = true;
        }

        ServiceHelper.SettingsService.Save();
        TuiApp.PostStatus("Patreon session cookie saved.");
    }

    private void TestPatreonCookie()
    {
        string cookie = ServiceHelper.SettingsService.Config.patreon_session_cookie;

        if (string.IsNullOrWhiteSpace(cookie))
        {
            TuiApp.PostStatus("No Patreon session cookie set. Use 'Set Patreon Session Cookie' first.");
            return;
        }

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus("Testing Patreon session cookie...");

            var diag = PatreonService.TestSessionCookie(cookie, "jotego");

            foreach (var message in diag.Messages)
            {
                TuiApp.PostStatus("  - " + message);
            }

            if (!diag.CookieValid)
            {
                TuiApp.PostStatus("Result: cookie is NOT valid. Grab a fresh session_id from your browser.");
            }
            else if (diag.IsPatron)
            {
                TuiApp.PostStatus("Result: cookie valid — you ARE a Jotego patron.");
            }
            else
            {
                TuiApp.PostStatus("Result: cookie works, but no active Jotego membership was detected.");
            }
        });
    }
}
