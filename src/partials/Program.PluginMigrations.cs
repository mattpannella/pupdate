using Pannella.Helpers;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    /// <summary>
    /// One-time notices for features that moved to plugins (queued by Config.Migrate()). Every
    /// pending feature gets a notice; the user is offered whatever is still needed to keep it working
    /// as before: installing the plugin and/or running it automatically during Update All.
    /// <paramref name="ask"/> and <paramref name="notify"/> are null when unattended: the notice is
    /// logged and the offer stays pending for the next interactive launch.
    /// Returns true when anything was shown.
    /// </summary>
    internal static bool OfferPluginMigrations(Func<string, bool> ask, Action<string> notify, Action<string> log)
    {
        var settings = ServiceHelper.SettingsService;
        var pending = settings.Config.pending_plugin_migrations?.ToList();

        if (pending == null || pending.Count == 0)
        {
            return false;
        }

        bool interactive = ask != null && notify != null;
        bool changed = false;
        bool shown = false;

        foreach (string pluginId in pending)
        {
            var migration = RetiredSettingMigrations.Find(pluginId);

            // Unknown id, or a feature that never applied here (e.g. Jotego names with no Jotego cores).
            if (migration == null || (migration.RequiresInstalledCorePrefix != null && !ServiceHelper.CoresService
                    .GetInstalledCoreIds()
                    .Any(id => id.StartsWith(migration.RequiresInstalledCorePrefix, StringComparison.OrdinalIgnoreCase))))
            {
                settings.Config.ClearPendingPluginMigration(pluginId);
                changed = true;
                continue;
            }

            shown = true;
            var entry = ServiceHelper.CoresService.GetPluginCatalogEntry(pluginId);

            if (entry == null)
            {
                log($"{migration.Message} The plugin catalog could not be loaded, so you'll be asked again next time.");
                continue;
            }

            var phase = entry.phase ?? PluginPhase.after;
            var installed = ServiceHelper.PluginService.FindByRepo(entry.Slug);
            bool autoRuns = installed != null &&
                            settings.GetPluginSettings(installed.DirectoryName).run_on_update;
            string when = $"automatically during Update All ({phase} cores)";
            string later = $"You can set it up later from Plugins, or with: pupdate plugins -i {entry.id}";

            if (!interactive)
            {
                log(autoRuns
                    ? $"{migration.Message} The {entry.name} plugin is installed and runs {when}."
                    : $"{migration.Message} {later}");

                if (autoRuns)
                {
                    settings.Config.ClearPendingPluginMigration(pluginId);
                    changed = true;
                }

                continue;
            }

            if (autoRuns)
            {
                notify($"{migration.Message}\n\nThe {entry.name} plugin is already installed and runs {when}, " +
                       "so nothing else is needed.");
            }
            else if (installed != null)
            {
                if (ask($"{migration.Message}\n\nThe {entry.name} plugin is already installed. Run it {when}?"))
                {
                    settings.SetPluginAutoRun(installed.DirectoryName, installed.Repo ?? entry.Slug, true, phase);
                    log($"{entry.name} will run {when}.");
                }
                else
                {
                    log(later);
                }
            }
            else if (ask($"{migration.Message}\n\nInstall the {entry.name} plugin and run it {when}?"))
            {
                if (ServiceHelper.PluginService.InstallFromGithub(entry.Slug))
                {
                    settings.SetPluginAutoRun(PluginService.DirectoryNameFor(entry.Slug), entry.Slug, true, phase);
                    log($"{entry.name} installed; it will run {when}.");
                }
                else
                {
                    log($"{entry.name} could not be installed. {later}");
                }
            }
            else
            {
                log(later);
            }

            settings.Config.ClearPendingPluginMigration(pluginId);
            changed = true;
        }

        if (changed)
        {
            settings.Save();
        }

        return shown;
    }
}
