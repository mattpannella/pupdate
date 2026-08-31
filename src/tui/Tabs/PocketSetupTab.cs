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
/// Pocket Setup tab: things done TO the Pocket - display modes, image/palette downloads, file
/// generators, Analogizer config, and Super GameBoy aspect ratio. pupdate's own configuration
/// (tokens, directories, thresholds, toggles) lives on the Settings tab. Built from the shared
/// components (ActionMenuTab + SubMenuDialog + the dialog/prompt set); related actions are grouped
/// behind submenu entries (…) to keep the list tidy, mirroring the classic menu structure.
/// </summary>
public sealed class PocketSetupTab : ActionMenuTab
{
    public PocketSetupTab(TuiContext context) : base(context, "Pocket Setup")
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
}
