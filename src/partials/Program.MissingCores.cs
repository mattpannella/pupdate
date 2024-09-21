using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella;

internal partial class Program
{
    private static void CheckForMissingCores(bool enableMissingCores)
    {
        if (ServiceHelper.SettingsService.GetMissingCores().Any())
        {
            Console.WriteLine("\nNew cores found since the last run.");
            AskAboutNewCores();

            string downloadNewCores = ServiceHelper.SettingsService.GetConfig().download_new_cores?.ToLowerInvariant();

            switch (downloadNewCores)
            {
                case "yes":
                    Console.WriteLine("The following cores have been enabled:");

                    foreach (Core core in ServiceHelper.SettingsService.GetMissingCores())
                    {
                        Console.WriteLine($"- {core.identifier}");
                    }

                    ServiceHelper.SettingsService.EnableMissingCores();
                    ServiceHelper.SettingsService.Save();
                    break;

                case "no":
                    Console.WriteLine("The following cores have been disabled:");

                    foreach (Core core in ServiceHelper.SettingsService.GetMissingCores())
                    {
                        Console.WriteLine($"- {core.identifier}");
                    }

                    ServiceHelper.SettingsService.DisableMissingCores();
                    ServiceHelper.SettingsService.Save();
                    break;

                default:
                    ServiceHelper.SettingsService.EnableMissingCores();

                    if (enableMissingCores)
                    {
                        ServiceHelper.SettingsService.Save();
                    }
                    else
                    {
                        RunCoreSelector(ServiceHelper.SettingsService.GetMissingCores(), "New cores are available!", true);
                    }

                    break;
            }

            // Is reloading the settings file necessary?
            ServiceHelper.ReloadSettings();
        }
    }
}
