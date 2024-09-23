using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella;

internal partial class Program
{
    private static Dictionary<string, bool> ShowCoresMenu(List<Core> cores, string message, bool isCoreSelection,
        bool skipQuit = false)
    {
        const int pageSize = 12;
        var offset = 0;
        bool more = true;
        var results = new Dictionary<string, bool>();
        string save,
            quit;

        if (isCoreSelection)
        {
            save = "Save Choices";
            quit = "Quit without saving";
        }
        else
        {
            save = "Apply Choices";
            quit = "Quit without applying";
        }

        while (more)
        {
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () => Console.WriteLine($"{message} Use enter to check/uncheck your choices.");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                    config.WriteItemAction = item => Console.Write("{0}", item.Name);
                });
            var current = -1;

            if ((offset + pageSize) <= cores.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            foreach (Core core in cores)
            {
                current++;

                if ((current <= (offset + pageSize)) && (current >= offset))
                {
                    var coreSettings = ServiceHelper.SettingsService.GetCoreSettings(core.identifier);
                    var selected =
                        (isCoreSelection && !coreSettings.skip) ||
                        (results.TryGetValue(core.identifier, out var result) && result);
                    var name = core.identifier;
                    var title = MenuItemName(name, selected, core.requires_license);

                    menu.Add(title, thisMenu =>
                    {
                        selected = !selected;

                        // ReSharper disable once RedundantDictionaryContainsKeyBeforeAdding
                        if (results.ContainsKey(core.identifier))
                        {
                            results[core.identifier] = selected;
                        }
                        else
                        {
                            results.Add(core.identifier, selected);
                        }

                        thisMenu.CurrentItem.Name = MenuItemName(core.identifier, selected, core.requires_license);
                    });
                }
            }

            if ((offset + pageSize) <= cores.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            if (offset != 0)
            {
                menu.Add("Prev Page", thisMenu =>
                {
                    offset -= pageSize;
                    thisMenu.CloseMenu();
                });
            }

            menu.Add(save, thisMenu =>
            {
                thisMenu.CloseMenu();
                more = false;
            });

            if (!skipQuit)
            {
                menu.Add(quit, thisMenu =>
                {
                    thisMenu.CloseMenu();
                    results.Clear();
                    more = false;
                });
            }

            menu.Show();
        }

        return results;
    }

    private static Dictionary<string, bool> RunCoreSelector(List<Core> cores, string message = "Select your cores.",
        bool skipQuit = false)
    {
        Dictionary<string, bool> results = null;

        if (ServiceHelper.SettingsService.GetConfig().download_new_cores?.ToLowerInvariant() == "yes")
        {
            foreach (Core core in cores)
            {
                ServiceHelper.SettingsService.EnableCore(core.identifier);
            }
        }
        else
        {
            results = ShowCoresMenu(cores, message, true, skipQuit);

            foreach (var item in results)
            {
                if (item.Value)
                    ServiceHelper.SettingsService.EnableCore(item.Key);
                else
                    ServiceHelper.SettingsService.DisableCore(item.Key);
            }
        }

        ServiceHelper.SettingsService.Save();

        return results;
    }
}
