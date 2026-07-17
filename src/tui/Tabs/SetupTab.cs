using System;
using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.DisplayModes;
using Pannella.Models.PocketLibraryImages;
using Pannella.Services;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Setup tab: display modes, image/palette downloads, file generators, directory locations, GitHub
/// token, Patreon config, and Super GameBoy aspect ratio. Built from the shared components
/// (ActionMenuTab + SubMenuDialog + the dialog/prompt set). Related actions are grouped behind
/// submenu entries (…) to keep the list tidy, mirroring the classic menu structure. Analogizer
/// lands in a later batch.
/// </summary>
public sealed class SetupTab : ActionMenuTab
{
    public SetupTab(TuiContext context) : base(context, "Setup")
    {
        AddAction("Display Modes…", DisplayModesMenu);
        AddAction("Download Images & Palettes…", DownloadsMenu);
        AddAction("Generate Files…", GenerateMenu);
        AddAction("Print openFPGA Categories", () =>
            Context.RunBackground(null, () => Program.PrintOpenFpgaCategories(TuiApp.PostStatus)));
        AddAction("Analogizer Config…", () => SubMenuDialog.Show("Analogizer Config", new (string, Action)[]
        {
            ("Standard Analogizer Config", AnalogizerWizard.RunStandard),
            ("Jotego Analogizer Config", AnalogizerWizard.RunJotego),
        }));
        AddAction("Set GitHub Token", SetGitHubToken);
        AddAction("Set Backup Saves Location", SetBackupLocation);
        AddAction("Set Archive Cache Location", SetArchiveCacheLocation);
        AddAction("Set Temp Directory", SetTempDirectory);
        AddAction("Set Patreon Session Cookie", SetPatreonCookie);
        AddAction("Test Patreon Session Cookie", TestPatreonCookie);
        AddAction("Super GameBoy: Apply 8:7 Aspect Ratio", () => ChangeSgbAspectRatio("8:7", 4, 3, 8, 7));
        AddAction("Super GameBoy: Restore 4:3 Aspect Ratio", () => ChangeSgbAspectRatio("4:3", 8, 7, 4, 3));
    }

    // ── Display Modes ────────────────────────────────────────────────────────────────────────

    private void DisplayModesMenu()
    {
        SubMenuDialog.Show("Display Modes", new (string, Action)[]
        {
            ("Enable Selected Display Modes (all cores)", () => SelectAndApplyDisplayModes(null)),
            ("Enable Selected Display Modes for Selected Cores", EnableDisplayModesForSelectedCores),
            ("Enable Recommended Display Modes", EnableRecommendedDisplayModes),
            ("Reset All Customized Display Modes", () => ResetDisplayModes(null)),
            ("Reset Selected Customized Display Modes", ResetSelectedDisplayModes),
            ("Change Merge/Overwrite Default Setting", ChangeDisplayModesSetting),
        });
    }

    private void EnableDisplayModesForSelectedCores()
    {
        var ids = CoreSelectorDialog.SelectSubset(ServiceHelper.CoresService.InstalledCores,
            "Which cores should get the selected display modes?");

        if (ids == null)
        {
            TuiApp.PostStatus("Cancelled.");
            return;
        }

        if (ids.Count == 0)
        {
            TuiApp.PostStatus("No cores selected.");
            return;
        }

        SelectAndApplyDisplayModes(ids);
    }

    // Show the display-mode picker, ask merge/overwrite, then apply to the given cores (null = all
    // non-skipped cores).
    private void SelectAndApplyDisplayModes(List<string> coreIds)
    {
        var values = DisplayModeSelectorDialog.Show(ServiceHelper.CoresService.AllDisplayModes);

        if (values == null)
        {
            TuiApp.PostStatus("Display mode selection cancelled.");
            return;
        }

        bool? merge = AskMergeOrOverwrite();

        if (merge == null)
        {
            TuiApp.PostStatus("Display mode update cancelled.");
            return;
        }

        var displayModeList = ServiceHelper.CoresService.ConvertDisplayModes(values);
        ApplyDisplayModes(coreIds ?? AllNonSkippedCoreIds(), displayModeList, isCurated: false, merge.Value);
    }

    private void EnableRecommendedDisplayModes()
    {
        bool? merge = ResolveCuratedMerge();

        if (merge == null)
        {
            TuiApp.PostStatus("Cancelled.");
            return;
        }

        ApplyDisplayModes(AllNonSkippedCoreIds(), displayModes: null, isCurated: true, merge.Value);
    }

    private void ApplyDisplayModes(List<string> coreIds, List<DisplayMode> displayModes, bool isCurated, bool merge)
    {
        Context.RunBackground(null, () =>
        {
            foreach (var id in coreIds)
            {
                TuiApp.PostStatus($"Updating display modes for {id}");
                ServiceHelper.CoresService.AddDisplayModes(id, displayModes, isCurated: isCurated, merge: merge);
            }

            ServiceHelper.SettingsService.Save();
            TuiApp.PostStatus($"Display modes updated for {coreIds.Count} core(s).");
        });
    }

    private void ResetSelectedDisplayModes()
    {
        var ids = CoreSelectorDialog.SelectSubset(ServiceHelper.CoresService.InstalledCores,
            "Which cores' display modes should be reset?");

        if (ids == null)
        {
            TuiApp.PostStatus("Cancelled.");
            return;
        }

        if (ids.Count == 0)
        {
            TuiApp.PostStatus("No cores selected.");
            return;
        }

        ResetDisplayModes(ids);
    }

    // Mirrors Program.ResetDisplayModes: restore each core's original display modes (or clear them)
    // and wipe the customized-display-modes settings. null = all cores with custom display modes.
    private void ResetDisplayModes(List<string> coreIds)
    {
        coreIds ??= ServiceHelper.CoresService.InstalledCoresWithCustomDisplayModes.Select(c => c.id).ToList();

        if (coreIds.Count == 0)
        {
            TuiApp.PostStatus("No cores have customized display modes.");
            return;
        }

        Context.RunBackground(null, () =>
        {
            foreach (var id in coreIds)
            {
                try
                {
                    var coreSettings = ServiceHelper.SettingsService.GetCoreSettings(id);

                    TuiApp.PostStatus($"Resetting display modes for {id}");

                    if (string.IsNullOrWhiteSpace(coreSettings.original_display_modes))
                    {
                        ServiceHelper.CoresService.ClearDisplayModes(id);
                    }
                    else
                    {
                        var original = coreSettings.original_display_modes.Split(',');
                        var modes = ServiceHelper.CoresService.ConvertDisplayModes(original);
                        ServiceHelper.CoresService.AddDisplayModes(id, modes);
                    }

                    coreSettings.display_modes = false;
                    coreSettings.original_display_modes = null;
                    coreSettings.selected_display_modes = null;
                }
                catch (Exception ex)
                {
                    TuiApp.PostStatus($"Error resetting {id}: {Util.GetExceptionMessage(ex)}");
                }
            }

            ServiceHelper.SettingsService.Save();
            TuiApp.PostStatus($"Reset display modes for {coreIds.Count} core(s).");
        });
    }

    private void ChangeDisplayModesSetting()
    {
        var config = ServiceHelper.SettingsService.Config;

        int? choice = SelectDialog.Show("Display Modes Default",
            "When applying display modes, by default:",
            new List<string> { "Merge with existing", "Overwrite existing", "Ask each time" });

        if (choice == null)
        {
            TuiApp.PostStatus("Setting unchanged.");
            return;
        }

        config.display_modes_option = choice switch { 0 => "merge", 1 => "overwrite", _ => "ask" };
        ServiceHelper.SettingsService.Save();
        TuiApp.PostStatus($"Display modes default set to '{config.display_modes_option}'.");
    }

    // Curated path respects the saved default; only prompts when it's "ask" or unset.
    private bool? ResolveCuratedMerge()
    {
        string option = ServiceHelper.SettingsService.Config.display_modes_option;

        return option switch
        {
            "merge" => true,
            "overwrite" => false,
            _ => AskMergeOrOverwrite()
        };
    }

    private bool? AskMergeOrOverwrite()
    {
        int choice = MessageBox.Query(App, "Display Modes",
            "Merge the selected display modes with existing ones, or overwrite?",
            "Merge", "Overwrite", "Cancel") ?? 2;

        return choice == 2 ? null : choice == 0;
    }

    private static List<string> AllNonSkippedCoreIds() =>
        ServiceHelper.CoresService.Cores
            .Where(core => !ServiceHelper.SettingsService.GetCoreSettings(core.id).skip)
            .Select(core => core.id)
            .ToList();

    // ── Downloads ────────────────────────────────────────────────────────────────────────────

    private void DownloadsMenu()
    {
        SubMenuDialog.Show("Download Images & Palettes", new (string, Action)[]
        {
            ("Download Platform Image Packs", DownloadPlatformImagePacks),
            ("Download Pocket Library Images", DownloadPocketLibraryImages),
            ("Download GameBoy Palettes", () =>
                Context.RunBackground(null, () => Program.DownloadGameBoyPalettes(TuiApp.PostStatus))),
        });
    }

    private void DownloadPlatformImagePacks()
    {
        var packs = ServiceHelper.PlatformImagePacksService.List;

        if (packs == null || packs.Count == 0)
        {
            TuiApp.PostStatus("No platform image packs found in catalog.");
            return;
        }

        var labels = packs.Select(p => string.IsNullOrWhiteSpace(p.variant)
            ? $"{p.owner}: {p.repository}"
            : $"{p.owner}: {p.repository} ({p.variant.Trim()})").ToList();

        int? choice = SelectDialog.Show("Platform Image Packs", "Select an image pack to install:", labels);

        if (choice == null)
        {
            return;
        }

        var pack = packs[choice.Value];

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus($"Installing image pack {pack.owner}/{pack.repository}...");
            ServiceHelper.PlatformImagePacksService.Install(pack.owner, pack.repository, pack.variant);
        });
    }

    private void DownloadPocketLibraryImages()
    {
        var items = new List<(string, Action)>
        {
            ("Spiritualized1997 (full set)", () =>
                Context.RunBackground(null, () => ServiceHelper.CoresService.DownloadPockLibraryImages())),
        };

        foreach (var menu in ServiceHelper.CoresService.PocketLibraryImagesList)
        {
            var m = menu;

            if (m?.entries == null || m.entries.Count == 0)
            {
                continue;
            }

            string title = string.IsNullOrWhiteSpace(m.menu_title) ? "(images)" : m.menu_title.Trim();
            items.Add(($"{title}…", () => ShowPocketLibraryImageEntries(m)));
        }

        SubMenuDialog.Show("Pocket Library Images", items);
    }

    private void ShowPocketLibraryImageEntries(PocketLibraryImageMenu menu)
    {
        var items = menu.entries.Select(entry =>
        {
            var img = entry;
            string label = string.IsNullOrWhiteSpace(img.menu_label) ? img.id : img.menu_label.Trim();

            return (label, (Action)(() =>
                Context.RunBackground(null, () => ServiceHelper.CoresService.DownloadPocketLibraryImages(img))));
        }).ToList();

        SubMenuDialog.Show(string.IsNullOrWhiteSpace(menu.menu_title) ? "Images" : menu.menu_title.Trim(), items);
    }

    // ── Generate Files ───────────────────────────────────────────────────────────────────────

    private void GenerateMenu()
    {
        SubMenuDialog.Show("Generate Files", new (string, Action)[]
        {
            ("Generate Instance JSON Files (PC Engine CD)", GenerateInstanceJson),
            ("Generate Game & Watch ROMs", () =>
                Context.RunBackground(null, () => Program.BuildGameAndWatchRoms(TuiApp.PostStatus))),
        });
    }

    private void GenerateInstanceJson()
    {
        bool overwrite = TuiPrompts.Confirm(App, "Instance JSON", "Overwrite existing instance JSON files?");

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus("Generating instance JSON files...");
            Context.CoreUpdater.BuildInstanceJson(overwrite);
            TuiApp.PostStatus("Instance JSON generation complete.");
        });
    }

    private void ChangeSgbAspectRatio(string label, int fromW, int fromH, int toW, int toH)
    {
        var sgbCores = ServiceHelper.CoresService.InstalledCores
            .Where(core => core.id.StartsWith("Spiritualized.SuperGB"))
            .ToList();

        if (sgbCores.Count == 0)
        {
            TuiApp.PostStatus("No Super GameBoy cores are installed.");
            return;
        }

        var ids = CoreSelectorDialog.SelectSubset(sgbCores, $"Which Super GameBoy cores → {label} aspect ratio?");

        if (ids == null)
        {
            TuiApp.PostStatus("Cancelled.");
            return;
        }

        if (ids.Count == 0)
        {
            TuiApp.PostStatus("No cores selected.");
            return;
        }

        Context.RunBackground(null, () =>
        {
            foreach (var id in ids)
            {
                TuiApp.PostStatus($"Updating {id} to {label}...");
                ServiceHelper.CoresService.ChangeAspectRatio(id, fromW, fromH, toW, toH);
            }

            TuiApp.PostStatus($"Aspect ratio updated for {ids.Count} core(s).");
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

            var diag = PatreonService.TestSessionCookie(cookie, "jotego", "jtbeta.zip");

            foreach (var message in diag.Messages)
            {
                TuiApp.PostStatus("  - " + message);
            }

            if (!diag.CookieValid)
            {
                TuiApp.PostStatus("Result: cookie is NOT valid. Grab a fresh session_id from your browser.");
            }
            else
            {
                switch (diag.AttachmentAccess)
                {
                    case PatreonService.AttachmentAccess.Accessible:
                        TuiApp.PostStatus("Result: cookie valid — your account can access the JT Beta post. Auto-fetch will work.");
                        break;
                    case PatreonService.AttachmentAccess.Gated:
                        TuiApp.PostStatus("Result: cookie valid, but your Patreon tier can't view the JT Beta post (tier may not include beta access).");
                        break;
                    case PatreonService.AttachmentAccess.NotFound:
                        TuiApp.PostStatus("Result: cookie valid, but no recent jtbeta.zip post was found. Try again after Jotego posts a new beta.");
                        break;
                    default:
                        TuiApp.PostStatus("Result: cookie valid, but the beta-access check couldn't be completed. See the lines above.");
                        break;
                }
            }
        });
    }
}
