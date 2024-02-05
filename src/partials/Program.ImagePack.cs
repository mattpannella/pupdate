using ConsoleTools;
using Pannella.Models;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static async Task ImagePackSelector(string path)
    {
        Console.Clear();
        Console.WriteLine("Checking for image packs...\n");

        ImagePack[] packs = await ImagePacksService.GetImagePacks();

        if (packs.Length > 0)
        {
            int choice = 0;
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    //config.EnableBreadcrumb = true;
                    config.WriteHeaderAction = () => Console.WriteLine("So, what'll it be?:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                });

            foreach (var pack in packs)
            {
                menu.Add($"{pack.owner}: {pack.repository} {pack.variant}", thisMenu =>
                {
                    choice = thisMenu.CurrentItem.Index;
                    thisMenu.CloseMenu();
                });
            }

            menu.Add("Go Back", thisMenu =>
            {
                choice = packs.Length;
                thisMenu.CloseMenu();
            });

            menu.Show();

            if (choice < packs.Length && choice >= 0)
            {
                await packs[choice].Install(path);
                Pause();
            }
            else if (choice == packs.Length)
            {
            }
            else
            {
                Console.WriteLine("You fucked up.");
                Pause();
            }
        }
        else
        {
            Console.WriteLine("None found. Have a nice day.");
            Pause();
        }
    }
}
