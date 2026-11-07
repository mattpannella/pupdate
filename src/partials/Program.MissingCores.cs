using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella;

internal static partial class Program
{
    // Longer lists of new cores are summarised instead of printed or paged through.
    private const int NEW_CORES_LIST_LIMIT = 15;

    private static void CheckForMissingCores(bool enableMissingCores, bool useTui = false)
    {
        if (FirstRunCoreSetupNeeded() && !AssumeYes)
        {
            // The full-screen UI asks the same question in a dialog once it opens.
            if (!useTui)
            {
                RunClassicFirstRunCoreSetup();
                ServiceHelper.ReloadSettings();
            }

            return;
        }

        if (ServiceHelper.SettingsService.GetMissingCores().Any())
        {
            Console.WriteLine("\nNew cores found since the last run.");
            AskAboutNewCores();

            string downloadNewCores = ServiceHelper.SettingsService.Config.download_new_cores?.ToLowerInvariant();

            switch (downloadNewCores)
            {
                case "yes":
                    Console.WriteLine("The following cores have been enabled:");
                    PrintCoreList(ServiceHelper.SettingsService.GetMissingCores());

                    ServiceHelper.SettingsService.EnableMissingCores();
                    ServiceHelper.SettingsService.Save();
                    break;

                case "no":
                    Console.WriteLine("The following cores have been disabled:");
                    PrintCoreList(ServiceHelper.SettingsService.GetMissingCores());

                    ServiceHelper.SettingsService.DisableMissingCores();
                    ServiceHelper.SettingsService.Save();
                    break;

                default:
                    ServiceHelper.SettingsService.EnableMissingCores();

                    if (enableMissingCores)
                    {
                        ServiceHelper.SettingsService.Save();
                    }
                    else if (ServiceHelper.SettingsService.GetMissingCores().Count > NEW_CORES_LIST_LIMIT)
                    {
                        int count = ServiceHelper.SettingsService.GetMissingCores().Count;

                        ServiceHelper.SettingsService.DisableMissingCores();
                        ServiceHelper.SettingsService.Save();
                        Console.WriteLine($"{count} new cores are available. They're turned off for now; " +
                                          "turn on the ones you want in Select Cores (the Cores tab in the full-screen UI).");
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

    private static void PrintCoreList(List<Core> cores)
    {
        foreach (Core core in cores.Take(NEW_CORES_LIST_LIMIT))
        {
            Console.WriteLine($"- {core.id}");
        }

        if (cores.Count > NEW_CORES_LIST_LIMIT)
        {
            Console.WriteLine($"...and {cores.Count - NEW_CORES_LIST_LIMIT} more.");
        }
    }
}
