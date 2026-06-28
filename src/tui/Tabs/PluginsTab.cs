using System.Linq;
using Pannella.Helpers;

namespace Pannella.TUI;

/// <summary>
/// Plugins tab. Dynamic: lists the discovered plugins (Enter runs one) plus management actions
/// (install from GitHub, check for updates, uninstall). Rebuilds its list after any change.
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
            AddAction($"Run: {descriptor.DisplayName}", () =>
                Context.RunBackground(null, () =>
                {
                    TuiApp.PostStatus($"Running {descriptor.DisplayName}...");
                    ServiceHelper.PluginService.Run(descriptor);
                    TuiApp.PostStatus($"{descriptor.DisplayName} finished.");
                }));
        }

        if (plugins.Count == 0)
        {
            AddAction("(no plugins installed)", () =>
                TuiApp.PostStatus($"No plugins installed in {ServiceHelper.PluginsDirectory}."));
        }

        AddAction("Install from GitHub…", InstallFromGithub);
        AddAction("Check for updates", CheckForUpdates);
        AddAction("Uninstall a plugin…", UninstallPlugin);
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
            TuiApp.PostStatus($"Uninstalled {descriptor.DisplayName}.");
            TuiHost.Invoke(Rebuild);
        });
    }
}
