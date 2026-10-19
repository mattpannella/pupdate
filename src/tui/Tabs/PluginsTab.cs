using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;

namespace Pannella.TUI;

/// <summary>
/// Plugins tab. Dynamic: lists the discovered plugins (Enter runs one) plus management actions
/// (browse the catalog, install from GitHub, auto-run config, check for updates, uninstall).
/// Rebuilds its list after any change.
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
            AddAction($"Run: {descriptor.DisplayName}{AutoSuffix(descriptor)}", () =>
                PluginRunModal.Run(descriptor.DisplayName, () => ServiceHelper.PluginService.Run(descriptor)));
        }

        if (plugins.Count == 0)
        {
            AddAction("(no plugins installed)", () =>
                TuiApp.PostStatus($"No plugins installed in {ServiceHelper.PluginsDirectory}."));
        }

        AddAction("Browse plugin catalog…", BrowseCatalog);
        AddAction("Install from GitHub…", InstallFromGithub);
        AddAction("Auto-run during Update All…", ConfigureAutoRun);
        AddAction("Check for updates", CheckForUpdates);
        AddAction("Uninstall a plugin…", UninstallPlugin);
    }

    private static string AutoSuffix(PluginDescriptor plugin)
    {
        var settings = ServiceHelper.SettingsService.GetPluginSettings(plugin.DirectoryName);

        return settings.run_on_update ? $"  [auto: {settings.phase} cores]" : string.Empty;
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

    private void ConfigureAutoRun()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        if (plugins.Count == 0)
        {
            TuiApp.PostStatus("No plugins installed.");
            return;
        }

        var settingsService = ServiceHelper.SettingsService;

        int? index = SelectDialog.Show("Auto-run during Update All", "Select a plugin:",
            plugins.Select(p => $"{p.DisplayName}{AutoSuffix(p)}").ToList());

        if (index == null)
        {
            return;
        }

        var descriptor = plugins[index.Value];

        int? choice = SelectDialog.Show(descriptor.DisplayName,
            "When should this plugin run during 'Update All'?",
            new[]
            {
                "Off",
                "Before cores (for plugins that fetch license keys)",
                "After cores (everything else)"
            });

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
            ? $"{descriptor.DisplayName}: auto-run off."
            : $"{descriptor.DisplayName}: runs {(choice.Value == 1 ? "before" : "after")} cores during Update All.");
        Rebuild();
    }

    private void CheckForUpdates()
    {
        Context.RunBackground(null, () =>
        {
            var managed = ServiceHelper.PluginService.Discover()
                .Where(p => !string.IsNullOrEmpty(p.Repo))
                .ToList();

            if (managed.Count == 0)
            {
                TuiApp.PostStatus("No plugins have a recorded GitHub repo to check.");
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

            TuiApp.PostStatus(updated == 0 ? "All plugins up to date." : $"Updated {updated} plugin(s).");

            if (updated > 0)
            {
                TuiHost.Invoke(Rebuild);
            }
        });
    }

    private void UninstallPlugin()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        if (plugins.Count == 0)
        {
            TuiApp.PostStatus("No plugins to uninstall.");
            return;
        }

        int? index = SelectDialog.Show("Uninstall Plugin", "Select a plugin to uninstall:",
            plugins.Select(p => $"{p.DisplayName}  ({p.DirectoryName})").ToList());

        if (index == null)
        {
            return;
        }

        var descriptor = plugins[index.Value];

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
