using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Plugins;

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
            menu.Add(descriptor.DisplayName, () =>
            {
                ServiceHelper.PluginService.Run(descriptor);
                Pause();
            });
        }

        menu.Add("Install from GitHub...", InstallPluginFromGithub);
        menu.Add("Check for updates", CheckAllPluginsForUpdates);
        menu.Add("Uninstall a plugin...", UninstallPluginMenu);
        menu.Add("Back", ConsoleMenu.Close);
        menu.Show();
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
