using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.DisplayModes;

namespace Pannella;

internal partial class Program
{
    private static void DisplayModeSelector(bool showCoreSelector = false)
    {
        Console.Clear();

        const int pageSize = 12;
        var offset = 0;
        var more = true;
        var count = 0;
        var results = new List<string>();
        var allDisplayModes = ServiceHelper.CoresService.GetAllDisplayModes();

        while (more)
        {
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () =>
                    {
                        Console.WriteLine("Which display modes would you like to enable?");
                        Console.WriteLine($"Note: There is a maximum of 16. You have {16 - count} remaining.");
                    };
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                    config.WriteItemAction = item => Console.Write("{0}", item.Name);
                });
            var current = -1;

            if ((offset + pageSize) <= allDisplayModes.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            foreach (DisplayMode displayMode in allDisplayModes)
            {
                current++;

                if ((current <= (offset + pageSize)) && (current >= offset))
                {
                    var selected = results.Contains(displayMode.value);
                    var title = MenuItemName(displayMode.description, selected);

                    menu.Add(title, thisMenu =>
                    {
                        if (count >= 16 && !selected)
                            return;

                        selected = !selected;

                        if (selected)
                        {
                            results.Add(displayMode.value);
                            count++;
                        }
                        else
                        {
                            results.Remove(displayMode.value);
                            count--;
                        }

                        thisMenu.CurrentItem.Name = MenuItemName(displayMode.description, selected);
                    });
                }
            }

            if ((offset + pageSize) <= allDisplayModes.Count)
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

            if (!showCoreSelector)
            {
                menu.Add("Apply Choices", thisMenu =>
                {
                    EnableDisplayModes(displayModes: results.ToArray());
                    thisMenu.CloseMenu();
                    more = false;
                });
            }
            else
            {
                menu.Add("Select Cores", thisMenu =>
                {
                    thisMenu.CloseMenu();
                    more = false;

                    var coreResults = ShowCoresMenu(
                        ServiceHelper.CoresService.InstalledCores,
                        "Which cores would you like to apply the selected display modes to?",
                        false);

                    EnableDisplayModes(
                        coreResults.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList(),
                        results.ToArray());
                });
            }

            menu.Add("Quit without applying", thisMenu =>
            {
                thisMenu.CloseMenu();
                more = false;
            });

            menu.Show();
        }
    }
}
