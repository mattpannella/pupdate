using ConsoleTools;
using Pannella.Helpers;

namespace Pannella;

internal static partial class Program
{
    private static void DisplayPluginsMenu()
    {
        var plugins = ServiceHelper.PluginService.Discover();

        if (plugins.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine($"No plugins found in: {ServiceHelper.PluginsDirectory}");
            Console.WriteLine("Drop *.wasm plugin files into that directory to make them available here.");
            Pause();
            return;
        }

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

        foreach (var plugin in plugins)
        {
            var descriptor = plugin;
            menu.Add(descriptor.DisplayName, () =>
            {
                ServiceHelper.PluginService.Run(descriptor);
                Pause();
            });
        }

        menu.Add("Back", ConsoleMenu.Close);
        menu.Show();
    }
}
