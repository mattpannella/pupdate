using ConsoleTools;
using Pannella.Helpers;

namespace Pannella;

internal partial class Program
{
    private static void PlatformImagePackSelector()
    {
        Console.Clear();

        if (ServiceHelper.PlatformImagePacksService.List.Count > 0)
        {
            int choice = 0;
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () => Console.WriteLine("So, what'll it be?:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                });

            foreach (var pack in ServiceHelper.PlatformImagePacksService.List)
            {
                menu.Add($"{pack.owner}: {pack.repository} {pack.variant ?? string.Empty}", thisMenu =>
                {
                    choice = thisMenu.CurrentItem.Index;
                    thisMenu.CloseMenu();
                });
            }

            menu.Add("Go Back", thisMenu =>
            {
                choice = ServiceHelper.PlatformImagePacksService.List.Count;
                thisMenu.CloseMenu();
            });

            menu.Show();

            if (choice < ServiceHelper.PlatformImagePacksService.List.Count && choice >= 0)
            {
                ServiceHelper.PlatformImagePacksService.Install(
                    ServiceHelper.PlatformImagePacksService.List[choice].owner,
                    ServiceHelper.PlatformImagePacksService.List[choice].repository,
                    ServiceHelper.PlatformImagePacksService.List[choice].variant);
            }
            else if (choice == ServiceHelper.PlatformImagePacksService.List.Count)
            {
                // What causes this?
            }
            else
            {
                Console.WriteLine("You fucked up.");
            }
        }
        else
        {
            Console.WriteLine("None found. Have a nice day.");
        }
    }
}
