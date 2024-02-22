using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella;

internal partial class Program
{
    private static void CheckForMissingCores(bool enableMissingCores)
    {
        if (GlobalHelper.SettingsService.GetMissingCores().Any())
        {
            Console.WriteLine("\nNew cores found since the last run.");
            AskAboutNewCores();

            string downloadNewCores = GlobalHelper.SettingsService.GetConfig().download_new_cores?.ToLowerInvariant();

            switch (downloadNewCores)
            {
                case "yes":
                    Console.WriteLine("The following cores have been enabled:");

                    foreach (Core core in GlobalHelper.SettingsService.GetMissingCores())
                    {
                        Console.WriteLine($"- {core.identifier}");
                    }

                    GlobalHelper.SettingsService.EnableMissingCores();
                    GlobalHelper.SettingsService.Save();
                    break;

                case "no":
                    Console.WriteLine("The following cores have been disabled:");

                    foreach (Core core in GlobalHelper.SettingsService.GetMissingCores())
                    {
                        Console.WriteLine($"- {core.identifier}");
                    }

                    GlobalHelper.SettingsService.DisableMissingCores();
                    GlobalHelper.SettingsService.Save();
                    break;

                default:
                    GlobalHelper.SettingsService.EnableMissingCores();

                    if (enableMissingCores)
                    {
                        GlobalHelper.SettingsService.Save();
                    }
                    else
                    {
                        RunCoreSelector(GlobalHelper.SettingsService.GetMissingCores(), "New cores are available!");
                    }

                    break;
            }

            // Is reloading the settings file necessary?
            GlobalHelper.ReloadSettings();
        }
    }
}
