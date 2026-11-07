using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;

namespace Pannella.TUI;

/// <summary>
/// Plugins tab. Dynamic: one row per installed plugin (Enter opens its menu: run, auto-run during
/// Update All, check for update, uninstall) plus the add/update actions. Rebuilds after any change.
/// </summary>
public sealed class PluginsTab : ActionMenuTab
{
    public PluginsTab(TuiContext context) : base(context, "Plugins")
    {
        Rebuild();
    }

    /// <summary>Re-discover and rebuild the list (e.g. when the tab is opened).</summary>
    public void Refresh() => Rebuild();

    private void Rebuild()
    {
        ClearActions();

        var plugins = ServiceHelper.PluginService.Discover();

        foreach (var plugin in plugins)
        {
            var descriptor = plugin;
            AddAction($"Plugin: {descriptor.DisplayName}{AutoSuffix(descriptor)}", () => ShowPluginMenu(descriptor));
        }

        if (plugins.Count == 0)
        {
            AddAction("(no plugins installed)", () =>
                TuiApp.PostStatus($"No plugins installed in {ServiceHelper.PluginsDirectory}."));
        }

        AddAction("Browse plugin catalog…", BrowseCatalog);
        AddAction("Install from GitHub…", InstallFromGithub);
        AddAction("Check for plugin updates", () => CheckForUpdates(null));
    }

    private static string AutoSuffix(PluginDescriptor plugin)
    {
        var settings = ServiceHelper.SettingsService.GetPluginSettings(plugin.DirectoryName);

        return settings.run_on_update ? $"  (runs {settings.phase} cores in Update All)" : string.Empty;
    }

    private static string AutoRunState(PluginDescriptor plugin)
    {
        var settings = ServiceHelper.SettingsService.GetPluginSettings(plugin.DirectoryName);

        return settings.run_on_update ? $"{settings.phase} cores" : "off";
    }

    private void ShowPluginMenu(PluginDescriptor descriptor)
    {
        SubMenuDialog.Show(descriptor.DisplayName, new (string, Action)[]
        {
            ("Run now", () => PluginRunModal.Run(descriptor.DisplayName, () => ServiceHelper.PluginService.Run(descriptor))),
            ($"Run during Update All: {AutoRunState(descriptor)}", () => ConfigureAutoRun(descriptor)),
            ("Check for update", () => CheckForUpdates(descriptor)),
            ("Uninstall", () => UninstallPlugin(descriptor)),
        });
    }

    private void BrowseCatalog()
    {
        Context.RunBackground(null, () =>
        {
            var catalog = ServiceHelper.CoresService.PluginCatalogList;

            if (catalog.Count == 0)
            {
                TuiApp.PostStatus("The plugin catalog is empty or could not be loaded.");
                return;
            }

            var installed = catalog
                .Select(entry => ServiceHelper.PluginService.FindByRepo(entry.Slug) != null)
                .ToList();

            TuiHost.Invoke(() => ShowCatalog(catalog, installed));
        });
    }

    private void ShowCatalog(List<PluginCatalogEntry> catalog, List<bool> installed)
    {
        var labels = catalog
            .Select((entry, i) => $"{(installed[i] ? "[installed] " : string.Empty)}{entry.name}" +
                                  (string.IsNullOrEmpty(entry.author) ? string.Empty : $" - {entry.author}"))
            .ToList();

        int? index = SelectDialog.Show("Plugin Catalog", "Select a plugin:", labels);

        if (index == null)
        {
            return;
        }

        var selected = catalog[index.Value];
        var links = new List<string> { selected.RepoUrl };
        links.AddRange(selected.links);

        string description = selected.description ?? string.Empty;

        description += selected.requires_input
            ? $"\n\nAsks questions when run (unattended runs skip it). Suggested phase: {selected.phase ?? PluginPhase.after} cores."
            : $"\n\nRuns without input. Suggested phase: {selected.phase ?? PluginPhase.after} cores.";

        if (installed[index.Value])
        {
            DetailsConfirmModal.Confirm(selected.name, description, links, "_OK");
            return;
        }

        if (!DetailsConfirmModal.Confirm(selected.name, description, links))
        {
            TuiApp.PostStatus($"{selected.name} not installed.");
            return;
        }

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus($"Installing plugin {selected.Slug}...");
            ServiceHelper.PluginService.InstallFromGithub(selected.Slug);
            TuiHost.Invoke(Rebuild);
        });
    }

    private void InstallFromGithub()
    {
        string spec = TuiPrompts.PromptText("Install Plugin",
            "GitHub repo (owner/repo) or full URL:");

        if (string.IsNullOrWhiteSpace(spec))
        {
            TuiApp.PostStatus("Plugin install cancelled.");
            return;
        }

        Context.RunBackground(null, () =>
        {
            TuiApp.PostStatus($"Installing plugin from {spec.Trim()}...");
            ServiceHelper.PluginService.InstallFromGithub(spec.Trim());
            TuiApp.PostStatus("Plugin install complete.");
            TuiHost.Invoke(Rebuild);
        });
    }

    private void ConfigureAutoRun(PluginDescriptor descriptor)
    {
        var settingsService = ServiceHelper.SettingsService;
        var current = settingsService.GetPluginSettings(descriptor.DirectoryName);
        int currentIndex = !current.run_on_update ? 0 : current.phase == PluginPhase.before ? 1 : 2;

        var options = new List<string>
        {
            "Off",
            "Before cores (for plugins that fetch license keys)",
            "After cores (everything else)"
        };
        options[currentIndex] += "  (current)";

        int? choice = SelectDialog.Show(descriptor.DisplayName,
            "Run this plugin automatically during 'Update All'?", options);

        if (choice == null)
        {
            return;
        }

        switch (choice.Value)
        {
            case 1:
                settingsService.SetPluginAutoRun(descriptor.DirectoryName, descriptor.Repo, true, PluginPhase.before);
                break;
            case 2:
                settingsService.SetPluginAutoRun(descriptor.DirectoryName, descriptor.Repo, true, PluginPhase.after);
                break;
            default:
                settingsService.SetPluginAutoRun(descriptor.DirectoryName, null, false);
                break;
        }

        settingsService.Save();
        TuiApp.PostStatus(choice.Value == 0
            ? $"{descriptor.DisplayName}: won't run during Update All."
            : $"{descriptor.DisplayName}: runs {(choice.Value == 1 ? "before" : "after")} cores during Update All.");
        Rebuild();
    }

    private void CheckForUpdates(PluginDescriptor only)
    {
        Context.RunBackground(null, () =>
        {
            var managed = (only != null ? new List<PluginDescriptor> { only } : ServiceHelper.PluginService.Discover())
                .Where(p => !string.IsNullOrEmpty(p.Repo))
                .ToList();

            if (managed.Count == 0)
            {
                TuiApp.PostStatus(only != null
                    ? $"{only.DisplayName} has no recorded GitHub repo to check."
                    : "No plugins have a recorded GitHub repo to check.");
                return;
            }

            int updated = 0;

            foreach (var plugin in managed)
            {
                TuiApp.PostStatus($"Checking {plugin.DisplayName} ({plugin.Repo})...");
                var newTag = ServiceHelper.PluginService.CheckForUpdate(plugin);

                if (newTag != null)
                {
                    TuiApp.PostStatus($"  {plugin.DisplayName}: {plugin.InstalledTag} → {newTag}; updating...");
                    ServiceHelper.PluginService.Update(plugin);
                    updated++;
                }
            }

            TuiApp.PostStatus(updated == 0
                ? (only != null ? $"{only.DisplayName} is up to date." : "All plugins up to date.")
                : $"Updated {updated} plugin(s).");

            if (updated > 0)
            {
                TuiHost.Invoke(Rebuild);
            }
        });
    }

    private void UninstallPlugin(PluginDescriptor descriptor)
    {
        if (!TuiPrompts.Confirm(App, "Uninstall Plugin", $"Uninstall '{descriptor.DisplayName}'?"))
        {
            return;
        }

        Context.RunBackground(null, () =>
        {
            ServiceHelper.PluginService.Uninstall(descriptor);
            ServiceHelper.SettingsService.SetPluginAutoRun(descriptor.DirectoryName, null, false);
            ServiceHelper.SettingsService.Save();
            TuiApp.PostStatus($"Uninstalled {descriptor.DisplayName}.");
            TuiHost.Invoke(Rebuild);
        });
    }
}
