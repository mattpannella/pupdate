using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;
using Pannella.Services;

namespace Pannella.TUI;

/// <summary>
/// Maintenance tab: bulk core operations (update/install/reinstall/uninstall selected, reinstall
/// all), backups, save-state pruning, version pinning, archive cache, and the ROM-set / platform
/// archive managers. Select-and-act items reuse CoreSelectorDialog's subset mode; the heavy work
/// runs on a background task.
/// </summary>
public sealed class MaintenanceTab : ActionMenuTab
{
    public MaintenanceTab(TuiContext context) : base(context, "Maintenance")
    {
        AddAction("Reinstall All Cores", ReinstallAll);
        AddAction("Update Selected", UpdateSelected);
        AddAction("Install Selected", InstallSelected);
        AddAction("Reinstall Selected", ReinstallSelected);
        AddAction("Uninstall Selected", UninstallSelected);
        AddAction("Pin / Unpin Core Version", PinCoreVersion);
        AddAction("Backup Saves & Memories", BackupSavesAndMemories);
        AddAction("Prune Save States", PruneSaveStates);
        AddAction("Clear Archive Cache", ClearCache);
        AddAction("Manage ROM Set Archives…", ManageRomSetArchives);
        AddAction("Archive / Unarchive Platforms…", ArchivePlatforms);
        AddAction("Archive Unused Platforms", ArchiveUnusedPlatforms);
    }

    // ── ROM Set Archives ─────────────────────────────────────────────────────────────────────

    private void ManageRomSetArchives()
    {
        SubMenuDialog.Show("ROM Set Archives", new (string, Action)[]
        {
            ("Sync with latest definitions", () => Context.RunBackground(null, () =>
            {
                TuiApp.PostStatus("Syncing archives...");
                ServiceHelper.SettingsService.SyncRomsets();
                TuiApp.PostStatus("Archives synced.");
            })),
            ("Enable / Disable archives…", EnableDisableArchives),
            ("Mark an archive incomplete…", MarkArchiveIncomplete),
        });
    }

    private static List<Archive> CoreSpecificArchives() =>
        ServiceHelper.SettingsService.Config.archives
            .Where(a => a.type == ArchiveType.core_specific_archive)
            .ToList();

    // Bulk enable/disable in one checklist (checked = enabled), replacing the old per-archive menus.
    private void EnableDisableArchives()
    {
        var archives = CoreSpecificArchives();

        if (archives.Count == 0)
        {
            TuiApp.PostStatus("No ROM set archives found. Sync definitions first.");
            return;
        }

        var labels = archives.Select(a => a.complete ? a.name : $"{a.name}  (incomplete)").ToList();

        var marked = ChecklistDialog.Show("ROM Set Archives",
            "Checked = enabled. Toggle archives on/off:",
            labels, i => archives[i].enabled, "Save");

        if (marked == null)
        {
            TuiApp.PostStatus("Cancelled.");
            return;
        }

        int changed = 0;

        for (int i = 0; i < archives.Count; i++)
        {
            bool wantEnabled = marked.Contains(i);

            if (wantEnabled != archives[i].enabled)
            {
                archives[i].enabled = wantEnabled;
                changed++;
            }
        }

        if (changed == 0)
        {
            TuiApp.PostStatus("No archive changes.");
            return;
        }

        ServiceHelper.SettingsService.Save();
        TuiApp.PostStatus($"Updated {changed} archive(s).");
    }

    // "Mark incomplete" is rare and one-way (forces a re-check next run), so it's a simple picker.
    private void MarkArchiveIncomplete()
    {
        var archives = CoreSpecificArchives().Where(a => a.complete).ToList();

        if (archives.Count == 0)
        {
            TuiApp.PostStatus("No complete archives to mark incomplete.");
            return;
        }

        int? choice = SelectDialog.Show("Mark Archive Incomplete",
            "Select an archive to mark incomplete (forces a re-check on next run):",
            archives.Select(a => a.name).ToList());

        if (choice == null)
        {
            return;
        }

        archives[choice.Value].complete = false;
        ServiceHelper.SettingsService.Save();
        TuiApp.PostStatus($"'{archives[choice.Value].name}' marked as incomplete.");
    }

    // ── Platform Archiving ───────────────────────────────────────────────────────────────────

    // Opens the platform checklist directly (checked = archived); no wrapper menu.
    private void ArchivePlatforms()
    {
        var platforms = ServiceHelper.CoresService.GetPlatforms();

        if (platforms.Count == 0)
        {
            TuiApp.PostStatus("No platforms found.");
            return;
        }

        int active = platforms.Count(p => !p.Archived);

        var labels = platforms.Select(p =>
        {
            string label = $"{p.Id} - {p.Name}";

            if (!p.Archived && !p.HasInstalledCore)
            {
                label += " [unused]";
            }

            return label;
        }).ToList();

        // Checked = archived. The checklist's type-ahead filter makes the long platform list quick
        // to navigate (e.g. type "unused" to find archivable ones).
        var marked = ChecklistDialog.Show("Archive Platforms",
            $"Active {active}/{CoresService.PLATFORM_LIMIT}. Checked = archived:",
            labels, i => platforms[i].Archived, "Apply");

        if (marked == null)
        {
            TuiApp.PostStatus("Cancelled.");
            return;
        }

        var changes = new List<(string id, bool archive)>();

        for (int i = 0; i < platforms.Count; i++)
        {
            bool wantArchived = marked.Contains(i);

            if (wantArchived != platforms[i].Archived)
            {
                changes.Add((platforms[i].Id, wantArchived));
            }
        }

        if (changes.Count == 0)
        {
            TuiApp.PostStatus("No platform changes.");
            return;
        }

        Context.RunBackground(null, () =>
        {
            foreach (var (id, archive) in changes)
            {
                if (archive)
                {
                    ServiceHelper.CoresService.ArchivePlatform(id);
                }
                else
                {
                    ServiceHelper.CoresService.UnarchivePlatform(id);
                }
            }

            TuiApp.PostStatus($"Applied {changes.Count} platform change(s).");
        });
    }

    // Quick path to get under the platform limit: archive every platform with no installed core.
    private void ArchiveUnusedPlatforms()
    {
        int unused = ServiceHelper.CoresService.GetPlatforms()
            .Count(p => !p.Archived && !p.HasInstalledCore);

        if (unused == 0)
        {
            TuiApp.PostStatus("No unused platforms to archive.");
            return;
        }

        if (!TuiPrompts.Confirm(App, "Archive Unused Platforms",
                $"Archive {unused} platform(s) that have no installed core?"))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            int archived = ServiceHelper.CoresService.ArchiveUnusedPlatforms();
            TuiApp.PostStatus($"Archived {archived} unused platform(s).");
        });
    }

    private void BackupSavesAndMemories()
    {
        string location = ServiceHelper.SettingsService.Config.backup_saves_location;

        Context.RunBackground(null, () =>
        {
            AssetsService.BackupSaves(ServiceHelper.UpdateDirectory, location, TuiApp.PostStatus);
            AssetsService.BackupMemories(ServiceHelper.UpdateDirectory, location, TuiApp.PostStatus);
            TuiApp.PostStatus("Backup complete.");
        });
    }

    private void PruneSaveStates()
    {
        if (!TuiPrompts.Confirm(App, "Prune Save States",
                "Back up Memories, then delete all but the most recent save state per game?"))
        {
            return;
        }

        Context.RunBackground(null, () =>
            AssetsService.PruneSaveStates(ServiceHelper.UpdateDirectory, null, TuiApp.PostStatus));
    }

    private void ReinstallAll()
    {
        if (!TuiPrompts.Confirm(App, "Reinstall All Cores",
                "Re-download and reinstall ALL cores? This can take a while."))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus("Starting clean reinstall of all cores...");
            int errors = Context.CoreUpdater.RunUpdates(null, clean: true, onlyUpdatedAssets: false);
            TuiApp.PostStatus(errors > 0 ? $"Reinstall finished with {errors} error(s)." : "Reinstall complete.");
        });
    }

    private void UpdateSelected()
    {
        var ids = CoreSelectorDialog.SelectSubset(ServiceHelper.CoresService.InstalledCores,
            "Which cores would you like to update?");

        if (!Confirmed(ids, out var list))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus($"Updating {list.Length} core(s)...");
            int errors = Context.CoreUpdater.RunUpdates(list, clean: false, onlyUpdatedAssets: false);
            TuiApp.PostStatus(errors > 0 ? $"Finished with {errors} error(s)." : "Update complete.");
        });
    }

    private void ReinstallSelected()
    {
        var ids = CoreSelectorDialog.SelectSubset(ServiceHelper.CoresService.InstalledCores,
            "Which cores would you like to reinstall?");

        if (!Confirmed(ids, out var list))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus($"Reinstalling {list.Length} core(s)...");
            int errors = Context.CoreUpdater.RunUpdates(list, clean: true, onlyUpdatedAssets: false);
            TuiApp.PostStatus(errors > 0 ? $"Finished with {errors} error(s)." : "Reinstall complete.");
        });
    }

    private void InstallSelected()
    {
        var ids = CoreSelectorDialog.SelectSubset(ServiceHelper.CoresService.CoresNotInstalled,
            "Which cores would you like pupdate to install and manage?");

        if (!Confirmed(ids, out var list))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            foreach (var id in list)
            {
                ServiceHelper.SettingsService.EnableCore(id);
            }

            ServiceHelper.SettingsService.Save();

            TuiApp.PostStatus($"Installing {list.Length} core(s)...");
            int errors = Context.CoreUpdater.RunUpdates(list, clean: false, onlyUpdatedAssets: false);
            TuiApp.PostStatus(errors > 0 ? $"Finished with {errors} error(s)." : "Install complete.");
        });
    }

    private void UninstallSelected()
    {
        var ids = CoreSelectorDialog.SelectSubset(ServiceHelper.CoresService.InstalledCores,
            "Which cores would you like to uninstall?");

        if (!Confirmed(ids, out var list))
        {
            return;
        }

        bool nuke = TuiPrompts.Confirm(App, "Uninstall",
            "Also delete the core-specific Assets folder for these cores?");

        Context.RunBackground(null, () =>
        {
            foreach (var id in list)
            {
                var core = ServiceHelper.CoresService.GetCore(id);

                if (core != null)
                {
                    TuiApp.PostStatus($"Uninstalling {id}...");
                    Context.CoreUpdater.DeleteCore(core, force: true, nuke: nuke);
                }
            }

            TuiApp.PostStatus($"Uninstalled {list.Length} core(s).");
        });
    }

    private void PinCoreVersion()
    {
        var settings = ServiceHelper.SettingsService;
        var cores = ServiceHelper.CoresService.InstalledCores.Where(c => c.repository != null).ToList();

        if (cores.Count == 0)
        {
            TuiApp.PostStatus("No pinnable cores installed.");
            return;
        }

        int? coreIndex = SelectDialog.Show("Pin / Unpin Core Version", "Select a core:",
            cores.Select(c =>
            {
                var pin = settings.GetCoreSettings(c.id).pinned_version;
                return pin == null ? c.id : $"{c.id}  (pinned: {pin})";
            }).ToList());

        if (coreIndex == null)
        {
            return;
        }

        var core = cores[coreIndex.Value];

        // Releases are already on the core object — no fetch needed. Build label/version pairs.
        var releases = new List<(string label, string version)>();

        if (core.releases != null)
        {
            foreach (var release in core.releases
                         .Where(r => r.core?.metadata != null)
                         .OrderByDescending(r => r.core.metadata.date_release ?? ""))
            {
                string version = release.core.metadata.version ?? "?";
                string date = release.core.metadata.date_release ?? "";
                releases.Add((string.IsNullOrEmpty(date) ? version : $"{version} ({date})", version));
            }
        }

        var options = new List<string> { "(Unpin — track latest)" };
        options.AddRange(releases.Select(r => r.label));

        string current = settings.GetCoreSettings(core.id).pinned_version;

        int? choice = SelectDialog.Show($"Pin {core.id}",
            $"Currently {(current == null ? "not pinned" : "pinned to " + current)}. Pick a version:", options);

        if (choice == null)
        {
            return;
        }

        if (choice.Value == 0)
        {
            settings.UnpinCoreVersion(core.id);
            settings.Save();
            TuiApp.PostStatus($"{core.id}: version pin removed.");
        }
        else
        {
            string version = releases[choice.Value - 1].version;
            settings.PinCoreVersion(core.id, version);
            settings.Save();
            TuiApp.PostStatus($"{core.id}: pinned to {version}.");
        }
    }

    private void ClearCache()
    {
        if (!ServiceHelper.SettingsService.Config.cache_archive_files)
        {
            TuiApp.PostStatus("Archive caching is not enabled.");
            return;
        }

        string cacheDir = ServiceHelper.CacheDirectory;

        if (!Directory.Exists(cacheDir))
        {
            TuiApp.PostStatus("Cache directory is already empty.");
            return;
        }

        if (!TuiPrompts.Confirm(App, "Clear Archive Cache", "Delete all cached archive files?"))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            Directory.Delete(cacheDir, recursive: true);
            TuiApp.PostStatus("Archive cache cleared.");
        });
    }

    // Shared guard for the select-and-act items: null = cancelled, empty = nothing picked.
    private static bool Confirmed(List<string> ids, out string[] list)
    {
        if (ids == null)
        {
            list = null;
            TuiApp.PostStatus("Cancelled.");
            return false;
        }

        if (ids.Count == 0)
        {
            list = null;
            TuiApp.PostStatus("No cores selected.");
            return false;
        }

        list = ids.ToArray();
        return true;
    }
}
