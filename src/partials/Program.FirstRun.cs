using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella;

internal static partial class Program
{
    internal static bool FirstRunCoreSetupNeeded()
    {
        var settings = ServiceHelper.SettingsService;

        return !settings.HasCoreSelections && settings.GetMissingCores().Count > 0;
    }

    private static string Cores(int count) => count == 1 ? "1 core" : $"{count} cores";

    internal static int InstalledMissingCoreCount() =>
        ServiceHelper.SettingsService.GetMissingCores().Count(c => ServiceHelper.CoresService.IsInstalled(c.id));

    internal static List<(FirstRunChoice Choice, string Label)> FirstRunOptions()
    {
        int total = ServiceHelper.SettingsService.GetMissingCores().Count;
        int installed = InstalledMissingCoreCount();
        var options = new List<(FirstRunChoice, string)>();

        if (installed > 0)
        {
            options.Add((FirstRunChoice.KeepInstalled, $"Keep the {Cores(installed)} already on my SD card"));
        }

        options.Add((FirstRunChoice.ByType, "Choose by type (Console, Handheld, Arcade...)"));
        options.Add((FirstRunChoice.None, "Start with none, I'll choose later"));
        options.Add((FirstRunChoice.Everything, $"Install everything (all {total} cores)"));

        return options;
    }

    internal static string FirstRunQuestion() =>
        $"Congratulations on installing pupdate, wow. There are {ServiceHelper.SettingsService.GetMissingCores().Count} cores available. " +
        "What're ya buyin'?";

    internal static List<(string Type, int Count)> FirstRunTypes() =>
        FirstRunCoreSelection.Summarize(ServiceHelper.SettingsService.GetMissingCores());

    /// <summary>Turns the first-run choice into per-core settings and saves. Returns a summary line.</summary>
    internal static string ApplyFirstRunChoice(FirstRunChoice choice, ICollection<string> types = null)
    {
        var settings = ServiceHelper.SettingsService;
        int enabled = 0;

        foreach (Core core in settings.GetMissingCores())
        {
            if (FirstRunCoreSelection.ShouldEnable(core, choice, ServiceHelper.CoresService.IsInstalled, types))
            {
                settings.EnableCore(core.id);
                enabled++;
            }
            else
            {
                settings.DisableCore(core.id);
            }
        }

        settings.Config.download_new_cores = choice == FirstRunChoice.Everything ? "yes" : "ask";
        settings.ClearMissingCores();
        settings.Save();

        return enabled == 0
            ? "No cores selected yet. Pick the ones you want in the Cores tab."
            : $"{Cores(enabled)} selected. Run Update All to install them.";
    }

    private static void RunClassicFirstRunCoreSetup()
    {
        FirstRunChoice? choice = null;
        var menu = new ConsoleMenu()
            .Configure(config =>
            {
                config.Selector = "=>";
                config.EnableWriteTitle = false;
                config.WriteHeaderAction = () => Console.WriteLine(FirstRunQuestion());
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
            });

        foreach (var (option, label) in FirstRunOptions())
        {
            menu.Add(label, thisMenu =>
            {
                choice = option;
                thisMenu.CloseMenu();
            });
        }

        menu.Show();

        HashSet<string> types = null;

        if (choice == FirstRunChoice.ByType)
        {
            types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var typeMenu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () =>
                        Console.WriteLine("Pick the kinds of cores you want, then choose Done:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                });

            foreach (var (type, count) in FirstRunTypes())
            {
                string title = $"{type} ({count} cores)";

                typeMenu.Add(MenuItemName(title, false), thisMenu =>
                {
                    bool on = !types.Remove(type);

                    if (on)
                    {
                        types.Add(type);
                    }

                    thisMenu.CurrentItem.Name = MenuItemName(title, on);
                });
            }

            typeMenu.Add("Done", ConsoleMenu.Close);
            typeMenu.Show();
        }

        Console.WriteLine();
        Console.WriteLine(ApplyFirstRunChoice(choice ?? FirstRunChoice.None, types));
        Pause();
    }
}
