using Pannella.Helpers;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;
using Pannella.Options;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    private static void RunPlugins(PluginsOptions options)
    {
        var plugins = ServiceHelper.PluginService;

        if (options.List)
        {
            ListPlugins();
        }
        else if (options.Browse)
        {
            BrowsePluginCatalogCli();
        }
        else if (!string.IsNullOrWhiteSpace(options.Install))
        {
            var entry = ServiceHelper.CoresService.GetPluginCatalogEntry(options.Install);
            plugins.InstallFromGithub(entry?.Slug ?? options.Install.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(options.Run))
        {
            var descriptor = RequirePlugin(options.Run);

            if (descriptor != null)
            {
                plugins.Run(descriptor, AssumeYes);
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.Remove))
        {
            var descriptor = RequirePlugin(options.Remove);

            if (descriptor != null)
            {
                plugins.Uninstall(descriptor);
                ServiceHelper.SettingsService.SetPluginAutoRun(descriptor.DirectoryName, null, false);
                ServiceHelper.SettingsService.Save();
            }
        }
        else if (options.Update)
        {
            UpdatePluginsCli(options.Name);
        }
        else if (!string.IsNullOrWhiteSpace(options.SetAuto))
        {
            var descriptor = RequirePlugin(options.SetAuto);

            if (descriptor == null)
            {
                return;
            }

            if (!Enum.TryParse(options.Phase, ignoreCase: true, out PluginPhase phase))
            {
                Console.WriteLine($"Unknown phase '{options.Phase}'. Use 'before' or 'after'.");
                return;
            }

            ServiceHelper.SettingsService.SetPluginAutoRun(descriptor.DirectoryName, descriptor.Repo, true, phase);
            ServiceHelper.SettingsService.Save();
            Console.WriteLine($"{descriptor.DisplayName} will run {phase} cores during 'Update All'.");
        }
        else if (!string.IsNullOrWhiteSpace(options.UnsetAuto))
        {
            var descriptor = RequirePlugin(options.UnsetAuto);

            if (descriptor == null)
            {
                return;
            }

            ServiceHelper.SettingsService.SetPluginAutoRun(descriptor.DirectoryName, null, false);
            ServiceHelper.SettingsService.Save();
            Console.WriteLine($"{descriptor.DisplayName} will no longer run during 'Update All'.");
        }
        else
        {
            Console.WriteLine("Specify one of: -l, -b, -i <repo>, -r <name>, --remove <name>, -u, --set-auto <name>, --unset-auto <name>");
        }
    }

    private static PluginDescriptor RequirePlugin(string nameOrDirectory)
    {
        var descriptor = ServiceHelper.PluginService.FindByName(nameOrDirectory.Trim());

        if (descriptor == null)
        {
            Console.WriteLine($"Plugin '{nameOrDirectory}' is not installed. Use -l to list installed plugins.");
        }

        return descriptor;
    }

    private static void ListPlugins()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        if (plugins.Count == 0)
        {
            Console.WriteLine($"No plugins installed in: {ServiceHelper.PluginsDirectory}");
            return;
        }

        Console.WriteLine();

        foreach (var plugin in plugins)
        {
            Console.WriteLine(plugin.DisplayName);
            Console.WriteLine($"  {plugin.DirectoryName}  {plugin.InstalledTag ?? "(local)"}  auto: {PluginAutoLabel(plugin)}");

            if (!string.IsNullOrWhiteSpace(plugin.Description))
            {
                Console.WriteLine(Util.WordWrap(plugin.Description, 80, "    "));
            }

            Console.WriteLine();
        }
    }

    private static void BrowsePluginCatalogCli()
    {
        var catalog = ServiceHelper.CoresService.PluginCatalogList;

        if (catalog.Count == 0)
        {
            Console.WriteLine("The plugin catalog is empty or could not be loaded.");
            return;
        }

        Console.WriteLine();

        foreach (var entry in catalog)
        {
            bool installed = ServiceHelper.PluginService.FindByRepo(entry.Slug) != null;

            PrintPluginCatalogEntry(entry, installed);
        }
    }

    private static void PrintPluginCatalogEntry(PluginCatalogEntry entry, bool installed)
    {
        Console.WriteLine($"{entry.id}{(installed ? "  [installed]" : string.Empty)}");
        Console.WriteLine($"  {entry.name}{(string.IsNullOrEmpty(entry.author) ? string.Empty : $" by {entry.author}")}");

        if (!string.IsNullOrWhiteSpace(entry.description))
        {
            Console.WriteLine(Util.WordWrap(entry.description, 80, "    "));
        }

        Console.WriteLine($"    More info: {entry.RepoUrl}");

        foreach (var link in entry.links)
        {
            Console.WriteLine($"               {link}");
        }

        Console.WriteLine(entry.requires_input
            ? $"    Asks questions when run (unattended runs skip it); suggested phase: {entry.phase ?? PluginPhase.after} cores"
            : $"    Runs without input; suggested phase: {entry.phase ?? PluginPhase.after} cores");

        Console.WriteLine();
    }

    private static void UpdatePluginsCli(string name)
    {
        var plugins = ServiceHelper.PluginService.Discover()
            .Where(p => !string.IsNullOrEmpty(p.Repo))
            .Where(p => string.IsNullOrWhiteSpace(name) ||
                        string.Equals(p.DisplayName, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.DirectoryName, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (plugins.Count == 0)
        {
            Console.WriteLine("No matching plugins with a recorded GitHub repo to update.");
            return;
        }

        int updated = 0;

        foreach (var plugin in plugins)
        {
            Console.WriteLine($"Checking {plugin.DisplayName} ({plugin.Repo})...");
            string newTag = ServiceHelper.PluginService.CheckForUpdate(plugin);

            if (newTag == null)
            {
                continue;
            }

            Console.WriteLine($"  {plugin.InstalledTag} -> {newTag}");
            ServiceHelper.PluginService.Update(plugin);
            updated++;
        }

        Console.WriteLine(updated == 0 ? "All plugins up to date." : $"Updated {updated} plugin(s).");
    }

    private static string PluginAutoLabel(PluginDescriptor plugin)
    {
        var settings = ServiceHelper.SettingsService.GetPluginSettings(plugin.DirectoryName);

        return settings.run_on_update ? $"{settings.phase} cores" : "off";
    }
}
