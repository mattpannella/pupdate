using Pannella.Helpers;
using Pannella.Models.Plugins;

namespace Pannella;

internal partial class Program
{
    // Each installed plugin costs a GitHub API call, so launches only check once a day.
    private static readonly TimeSpan PLUGIN_UPDATE_CHECK_INTERVAL = TimeSpan.FromHours(24);

    internal static bool PluginUpdateCheckDue()
    {
        var last = ServiceHelper.SettingsService.Config.last_plugin_update_check;

        if (last.HasValue && DateTime.UtcNow - last.Value < PLUGIN_UPDATE_CHECK_INTERVAL)
        {
            return false;
        }

        return ServiceHelper.PluginService.Discover().Any(p => !string.IsNullOrEmpty(p.Repo));
    }

    /// <summary>
    /// Launch-time check for plugin updates. Asks before updating unless update_plugins is on.
    /// Returns true when anything was shown.
    /// </summary>
    internal static bool CheckPluginUpdatesOnLaunch(Func<string, bool> ask, Action<string> log)
    {
        if (!PluginUpdateCheckDue())
        {
            return false;
        }

        var settings = ServiceHelper.SettingsService;
        var plugins = ServiceHelper.PluginService;
        var updates = new List<(PluginDescriptor Plugin, string Tag)>();

        foreach (var plugin in plugins.Discover().Where(p => !string.IsNullOrEmpty(p.Repo)))
        {
            string tag = plugins.CheckForUpdate(plugin);

            if (tag != null)
            {
                updates.Add((plugin, tag));
            }
        }

        settings.Config.last_plugin_update_check = DateTime.UtcNow;
        settings.Save();

        if (updates.Count == 0)
        {
            return false;
        }

        string list = string.Join("\n", updates.Select(u => $"  {u.Plugin.DisplayName}: {u.Plugin.InstalledTag ?? "?"} -> {u.Tag}"));

        if (!settings.Config.update_plugins && !ask($"Plugin updates are available:\n{list}\n\nUpdate now?"))
        {
            log("Plugin updates skipped. You can update them later from the Plugins menu.");
            return true;
        }

        foreach (var (plugin, tag) in updates)
        {
            log($"Updating {plugin.DisplayName} to {tag}...");

            if (!plugins.Update(plugin))
            {
                log($"Could not update {plugin.DisplayName}; it is still on {plugin.InstalledTag}.");
            }
        }

        return true;
    }
}
