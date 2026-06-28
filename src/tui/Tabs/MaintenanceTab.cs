using System.IO;
using Pannella.Helpers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Maintenance tab. Starts with the low-risk actions that map cleanly onto existing services:
/// a clean reinstall (RunUpdates clean) and clearing the archive cache (small logic replicated
/// from Program.ClearArchiveCache so it uses TUI prompts/status instead of Console). Prune,
/// backups, pin-version and platform archiving land in later increments.
/// </summary>
public sealed class MaintenanceTab : FrameView
{
    public MaintenanceTab(TuiContext context)
    {
        Title = "Maintenance";

        var reinstall = new Button
        {
            X = 1,
            Y = 1,
            Text = "_Reinstall All Cores"
        };

        reinstall.Accepting += (_, e) =>
        {
            e.Handled = true;

            if (!TuiPrompts.Confirm(App, "Reinstall All Cores",
                    "Re-download and reinstall ALL cores? This can take a while."))
            {
                return;
            }

            context.RunBackground(reinstall, () =>
            {
                TuiApp.PostStatus("Starting clean reinstall of all cores...");
                int errors = context.CoreUpdater.RunUpdates(null, clean: true, onlyUpdatedAssets: false);
                TuiApp.PostStatus(errors > 0
                    ? $"Reinstall finished with {errors} error(s)."
                    : "Reinstall complete.");
            });
        };

        var clearCache = new Button
        {
            X = 1,
            Y = 3,
            Text = "_Clear Archive Cache"
        };

        clearCache.Accepting += (_, e) =>
        {
            e.Handled = true;

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

            context.RunBackground(clearCache, () =>
            {
                Directory.Delete(cacheDir, recursive: true);
                TuiApp.PostStatus("Archive cache cleared.");
            });
        };

        var hint = new Label
        {
            X = 1,
            Y = 5,
            Text = "More maintenance (prune save states, backups, pin versions, platforms) coming next."
        };

        Add(reinstall);
        Add(clearCache);
        Add(hint);
    }
}
