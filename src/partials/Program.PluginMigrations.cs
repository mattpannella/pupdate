using Pannella.Helpers;
using Pannella.Models.Settings;

namespace Pannella;

internal partial class Program
{
    /// <summary>
    /// One-time offers to install the plugins that replaced retired settings (queued by
    /// Config.Migrate()). <paramref name="prompt"/> shows the message and returns whether to
    /// install; null (unattended) prints a hint and leaves the offer pending for the next
    /// interactive launch.
    /// </summary>
    internal static void OfferPluginMigrations(Func<string, bool> prompt)
    {
        var settings = ServiceHelper.SettingsService;
        var pending = settings.Config.pending_plugin_migrations?.ToList();

        if (pending == null || pending.Count == 0)
        {
            return;
        }

        bool changed = false;

        foreach (string pluginId in pending)
        {
            var migration = RetiredSettingMigrations.Find(pluginId);

            if (migration == null)
            {
                settings.Config.ClearPendingPluginMigration(pluginId);
                changed = true;
                continue;
            }

            if (migration.RequiresInstalledCorePrefix != null && !ServiceHelper.CoresService.GetInstalledCoreIds()
                    .Any(id => id.StartsWith(migration.RequiresInstalledCorePrefix, StringComparison.OrdinalIgnoreCase)))
            {
                settings.Config.ClearPendingPluginMigration(pluginId);
                changed = true;
                continue;
            }

            var entry = ServiceHelper.CoresService.GetPluginCatalogEntry(pluginId);

            if (entry == null)
            {
                Console.WriteLine($"{migration.Feature}: this feature is now a plugin, but the plugin catalog " +
                                  "could not be loaded. You'll be asked again next time.");
                continue;
            }

            if (ServiceHelper.PluginService.FindByRepo(entry.Slug) != null)
            {
                settings.Config.ClearPendingPluginMigration(pluginId);
                changed = true;
                continue;
            }

            if (prompt == null)
            {
                Console.WriteLine($"{migration.Message} Install it with: pupdate plugins -i {entry.id}");
                continue;
            }

            if (prompt($"{migration.Message}\n\nInstall the {entry.name} plugin now?"))
            {
                ServiceHelper.PluginService.InstallFromGithub(entry.Slug);
            }
            else
            {
                Console.WriteLine("You can install it later from Plugins > Browse plugin catalog, " +
                                  $"or with: pupdate plugins -i {entry.id}");
            }

            settings.Config.ClearPendingPluginMigration(pluginId);
            changed = true;
        }

        if (changed)
        {
            settings.Save();
        }
    }
}
