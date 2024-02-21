using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella;

internal partial class Program
{
    private static void CheckForMissingCores(bool enableMissingCores)
    {
        if (GlobalHelper.SettingsManager.GetMissingCores().Any())
        {
            Console.WriteLine("\nNew cores found since the last run.");
            AskAboutNewCores();

            string downloadNewCores = GlobalHelper.SettingsManager.GetConfig().download_new_cores?.ToLowerInvariant();

            switch (downloadNewCores)
            {
                case "yes":
                    Console.WriteLine("The following cores have been enabled:");

                    foreach (Core core in GlobalHelper.SettingsManager.GetMissingCores())
                    {
                        Console.WriteLine($"- {core.identifier}");
                    }

                    GlobalHelper.SettingsManager.EnableMissingCores(GlobalHelper.SettingsManager.GetMissingCores());
                    GlobalHelper.SettingsManager.SaveSettings();
                    break;

                case "no":
                    Console.WriteLine("The following cores have been disabled:");

                    foreach (Core core in GlobalHelper.SettingsManager.GetMissingCores())
                    {
                        Console.WriteLine($"- {core.identifier}");
                    }

                    GlobalHelper.SettingsManager.DisableMissingCores(GlobalHelper.SettingsManager.GetMissingCores());
                    GlobalHelper.SettingsManager.SaveSettings();
                    break;

                default:
                    var newOnes = GlobalHelper.SettingsManager.GetMissingCores();

                    GlobalHelper.SettingsManager.EnableMissingCores(newOnes);

                    if (enableMissingCores)
                    {
                        GlobalHelper.SettingsManager.SaveSettings();
                    }
                    else
                    {
                        RunCoreSelector(newOnes, "New cores are available!");
                    }

                    break;
            }

            // Is reloading the settings file necessary?
            GlobalHelper.ReloadSettings();
        }
    }
}
