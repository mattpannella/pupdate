using Pannella.Helpers;
using Pannella.Models.DisplayModes;

namespace Pannella;

internal partial class Program
{
    private static void EnableDisplayModes(List<string> coreIdentifiers = null, List<DisplayMode> displayModes = null,
        bool isCurated = false)
    {
        AskAboutDisplayModesSetting();

        string answer = null;

        if (ServiceHelper.SettingsService.Config.display_modes_option == "ask")
        {
            answer = AskAboutDisplayModes();
        }

        coreIdentifiers ??= ServiceHelper.CoresService.Cores
            .Where(core => !ServiceHelper.SettingsService.GetCoreSettings(core.identifier).skip)
            .Select(core => core.identifier)
            .ToList();

        foreach (var coreIdentifier in coreIdentifiers)
        {
            try
            {
                Console.WriteLine($"Updating display modes for {coreIdentifier}");
                ServiceHelper.CoresService.AddDisplayModes(coreIdentifier, displayModes, isCurated,
                    merge: answer == "merge");
            }
            catch (Exception e)
            {
                Console.WriteLine("Uh oh something went wrong.");
                if (ServiceHelper.SettingsService.debug.show_stack_traces)
                {
                    Console.WriteLine(e.ToString());
                }
                else
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        ServiceHelper.SettingsService.Save();

        Console.WriteLine("Finished.");
    }

    private static void ResetDisplayModes(List<string> coreIdentifiers = null)
    {
        coreIdentifiers ??= ServiceHelper.CoresService.InstalledCoresWithCustomDisplayModes.Select(c => c.identifier)
            .ToList();

        foreach (var coreIdentifier in coreIdentifiers)
        {
            try
            {
                var coreSettings = ServiceHelper.SettingsService.GetCoreSettings(coreIdentifier);

                Console.WriteLine($"Resetting display modes for {coreIdentifier}");

                if (string.IsNullOrWhiteSpace(coreSettings.original_display_modes))
                {
                    ServiceHelper.CoresService.ClearDisplayModes(coreIdentifier);
                }
                else
                {
                    var originalDisplayModes = coreSettings.original_display_modes.Split(',');
                    var displayModes = ServiceHelper.CoresService.ConvertDisplayModes(originalDisplayModes);

                    ServiceHelper.CoresService.AddDisplayModes(coreIdentifier, displayModes);
                }

                coreSettings.display_modes = false;
                coreSettings.original_display_modes = null;
                coreSettings.selected_display_modes = null;
            }
            catch (Exception e)
            {
                Console.WriteLine("Uh oh something went wrong.");
                if (ServiceHelper.SettingsService.debug.show_stack_traces)
                {
                    Console.WriteLine(e.ToString());
                }
                else
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        ServiceHelper.SettingsService.Save();

        Console.WriteLine("Finished.");
    }
}
