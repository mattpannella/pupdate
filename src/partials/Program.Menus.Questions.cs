using Pannella.Helpers;

namespace Pannella;

internal static partial class Program
{
    private static void AskAboutNewCores(bool force = false)
    {
        while (ServiceHelper.SettingsService.Config.download_new_cores == null || force)
        {
            force = false;

            Console.WriteLine("Would you like to, by default, install new cores? [Y]es, [N]o, [A]sk for each:");

            ConsoleKey response = Console.ReadKey(true).Key;

            ServiceHelper.SettingsService.Config.download_new_cores = response switch
            {
                ConsoleKey.Y => "yes",
                ConsoleKey.N => "no",
                ConsoleKey.A => "ask",
                _ => null
            };
        }
    }

    private static void AskAboutDisplayModesSetting(bool force = false)
    {
        while (ServiceHelper.SettingsService.Config.display_modes_option == null || force)
        {
            force = false;

            Console.WriteLine("Would you like to, by default, merge or overwrite the display modes? [M]erge, [O]verwrite, [A]sk each time:");

            ConsoleKey response = Console.ReadKey(true).Key;

            ServiceHelper.SettingsService.Config.display_modes_option = response switch
            {
                ConsoleKey.M => "merge",
                ConsoleKey.O => "overwrite",
                ConsoleKey.A => "ask",
                _ => null
            };
        }

        ServiceHelper.SettingsService.Save();
    }

    private static string AskAboutDisplayModes()
    {
        string result = null;

        while (result == null)
        {
            Console.WriteLine("Would you like to merge or overwrite the display modes? [M]erge, [O]verwrite:");

            ConsoleKey response = Console.ReadKey(true).Key;

            result = response switch
            {
                ConsoleKey.M => "merge",
                ConsoleKey.O => "overwrite",
                _ => null
            };
        }

        return result;
    }

    private static bool AskYesNoQuestion(string question)
    {
        Console.WriteLine($"{question} [Y]es, [N]o");

        bool? result = null;

        while (result == null)
        {
            result = Console.ReadKey(true).Key switch
            {
                ConsoleKey.Y => true,
                ConsoleKey.N => false,
                _ => null
            };
        }

        return result.Value;
    }
}
