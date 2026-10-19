using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;

namespace Pannella;

internal static partial class Program
{
    private static void DisplayPluginsMenu()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        var menu = new ConsoleMenu()
            .Configure(c =>
            {
                c.Selector = "=>";
                c.EnableWriteTitle = false;
                c.EnableAlphabet = true;
                c.WriteHeaderAction = () =>
                {
                    Console.WriteLine("Plugins");
                    Console.WriteLine($"  ({ServiceHelper.PluginsDirectory})");
                };
                c.SelectedItemBackgroundColor = Console.ForegroundColor;
                c.SelectedItemForegroundColor = Console.BackgroundColor;
            });

        if (plugins.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine($"No plugins installed in: {ServiceHelper.PluginsDirectory}");
        }

        foreach (var plugin in plugins)
        {
            var descriptor = plugin;
            menu.Add(PluginMenuLabel(descriptor), () =>
            {
                ServiceHelper.PluginService.Run(descriptor);
                Pause();
            });
        }

        menu.Add("Browse plugin catalog...", BrowsePluginCatalogMenu);
        menu.Add("Install from GitHub...", InstallPluginFromGithub);
        menu.Add("Auto-run during Update All...", PluginAutoRunMenu);
        menu.Add("Check for updates", CheckAllPluginsForUpdates);
        menu.Add("Uninstall a plugin...", UninstallPluginMenu);
        menu.Add("Back", ConsoleMenu.Close);
        menu.Show();
    }

    private static string PluginMenuLabel(PluginDescriptor plugin)
    {
        var settings = ServiceHelper.SettingsService.GetPluginSettings(plugin.DirectoryName);

        return settings.run_on_update
            ? $"{plugin.DisplayName}  [auto: {settings.phase} cores]"
            : plugin.DisplayName;
    }

    private static void BrowsePluginCatalogMenu()
    {
        var catalog = ServiceHelper.CoresService.PluginCatalogList;

        if (catalog.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("The plugin catalog is empty or could not be loaded.");
            Pause();
            return;
        }

        var menu = new ConsoleMenu()
            .Configure(c =>
            {
                c.Selector = "=>";
                c.EnableWriteTitle = false;
                c.EnableAlphabet = true;
                c.WriteHeaderAction = () => Console.WriteLine("Plugin catalog");
                c.SelectedItemBackgroundColor = Console.ForegroundColor;
                c.SelectedItemForegroundColor = Console.BackgroundColor;
            });

        foreach (var entry in catalog)
        {
            var catalogEntry = entry;
            bool installed = ServiceHelper.PluginService.FindByRepo(catalogEntry.Slug) != null;
            string label = installed ? $"{catalogEntry.name}  (installed)" : catalogEntry.name;

            menu.Add(label, thisMenu =>
            {
                Console.WriteLine();
                PrintPluginCatalogEntry(catalogEntry, installed);

                if (installed)
                {
                    Pause();
                    return;
                }

                if (AskYesNoQuestion("Would you like to install this?"))
                {
                    ServiceHelper.PluginService.InstallFromGithub(catalogEntry.Slug);
                    Pause();
                    thisMenu.CloseMenu();
                }
            });
        }

        menu.Add("Back", ConsoleMenu.Close);
        menu.Show();
    }

    private static void PluginAutoRunMenu()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        if (plugins.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("No plugins installed.");
            Pause();
            return;
        }

        var settingsService = ServiceHelper.SettingsService;

        string Label(PluginDescriptor plugin)
        {
            var settings = settingsService.GetPluginSettings(plugin.DirectoryName);

            return settings.run_on_update
                ? $"[x] {plugin.DisplayName} - {settings.phase} cores"
                : $"[ ] {plugin.DisplayName}";
        }

        var menu = new ConsoleMenu()
            .Configure(c =>
            {
                c.Selector = "=>";
                c.EnableWriteTitle = false;
                c.EnableAlphabet = true;
                c.WriteHeaderAction = () =>
                {
                    Console.WriteLine("Auto-run during Update All. Enter cycles: off -> before cores -> after cores.");
                    Console.WriteLine("'before' is for plugins that fetch license keys; 'after' for everything else.");
                };
                c.SelectedItemBackgroundColor = Console.ForegroundColor;
                c.SelectedItemForegroundColor = Console.BackgroundColor;
                c.WriteItemAction = item => Console.Write("{0}", item.Name);
            });

        foreach (var plugin in plugins)
        {
            var descriptor = plugin;

            menu.Add(Label(descriptor), thisMenu =>
            {
                var current = settingsService.GetPluginSettings(descriptor.DirectoryName);

                if (!current.run_on_update)
                    settingsService.SetPluginAutoRun(descriptor.DirectoryName, descriptor.Repo, true, PluginPhase.before);
                else if (current.phase == PluginPhase.before)
                    settingsService.SetPluginAutoRun(descriptor.DirectoryName, descriptor.Repo, true, PluginPhase.after);
                else
                    settingsService.SetPluginAutoRun(descriptor.DirectoryName, null, false);

                thisMenu.CurrentItem.Name = Label(descriptor);
            });
        }

        menu.Add("Save", thisMenu => thisMenu.CloseMenu());
        menu.Show();

        settingsService.Save();
    }

    private static void UninstallPluginMenu()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        if (plugins.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("No plugins to uninstall.");
            Pause();
            return;
        }

        var menu = new ConsoleMenu()
            .Configure(c =>
            {
                c.Selector = "=>";
                c.EnableWriteTitle = false;
                c.EnableAlphabet = true;
                c.WriteHeaderAction = () => Console.WriteLine("Uninstall which plugin?");
                c.SelectedItemBackgroundColor = Console.ForegroundColor;
                c.SelectedItemForegroundColor = Console.BackgroundColor;
            });

        foreach (var plugin in plugins)
        {
            var d = plugin;
            menu.Add($"{d.DisplayName}  ({d.DirectoryName})", thisMenu =>
            {
                if (AskYesNoQuestion($"Uninstall '{d.DisplayName}'?"))
                {
                    ServiceHelper.PluginService.Uninstall(d);
                    ServiceHelper.SettingsService.SetPluginAutoRun(d.DirectoryName, null, false);
                    ServiceHelper.SettingsService.Save();
                    Pause();
                }
                thisMenu.CloseMenu();
            });
        }

        menu.Add("Cancel", ConsoleMenu.Close);
        menu.Show();
    }

    private static void InstallPluginFromGithub()
    {
        Console.WriteLine();
        Console.WriteLine("Enter GitHub repo (owner/repo) or full URL.");
        Console.WriteLine("Example: openfpga-library/pocket-plugin");
        Console.Write("> ");
        var spec = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(spec))
        {
            Console.WriteLine("Cancelled.");
            Pause();
            return;
        }

        ServiceHelper.PluginService.InstallFromGithub(spec.Trim());
        Pause();
    }

    private static void CheckAllPluginsForUpdates()
    {
        Console.WriteLine();
        var plugins = ServiceHelper.PluginService.Discover();
        var managed = plugins.Where(p => !string.IsNullOrEmpty(p.Repo)).ToList();

        if (managed.Count == 0)
        {
            Console.WriteLine("No plugins have a recorded GitHub repo to check.");
            Pause();
            return;
        }

        var updates = new List<(PluginDescriptor plugin, string newTag)>();
        foreach (var plugin in managed)
        {
            Console.WriteLine($"Checking {plugin.DisplayName} ({plugin.Repo})...");
            var newTag = ServiceHelper.PluginService.CheckForUpdate(plugin);
            if (newTag != null)
                updates.Add((plugin, newTag));
        }

        if (updates.Count == 0)
        {
            Console.WriteLine("All plugins up to date.");
            Pause();
            return;
        }

        foreach (var (plugin, newTag) in updates)
        {
            Console.WriteLine();
            Console.WriteLine($"  {plugin.DisplayName}: {plugin.InstalledTag} → {newTag}");
            if (AskYesNoQuestion("Update?"))
                ServiceHelper.PluginService.Update(plugin);
        }

        Pause();
    }
}
