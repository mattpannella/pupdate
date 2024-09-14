using Pannella.Helpers;

namespace Pannella;

internal partial class Program
{
    private static void EnableDisplayModes(List<string> coreIdentifiers = null, string[] displayModes = null, bool isCurated = false)
    {
        coreIdentifiers ??= ServiceHelper.CoresService.Cores
            .Where(core => !ServiceHelper.SettingsService.GetCoreSettings(core.identifier).skip)
            .Select(core => core.identifier)
            .ToList();

        foreach (var core in coreIdentifiers)
        {
            try
            {
                // not sure if this check is still needed
                if (core == null)
                {
                    Console.WriteLine("Core Name is required. Skipping.");
                    continue;
                }

                Console.WriteLine("Updating " + core);
                ServiceHelper.CoresService.AddDisplayModes(core, displayModes, isCurated);
                ServiceHelper.SettingsService.Save();
            }
            catch (Exception e)
            {
                Console.WriteLine("Uh oh something went wrong.");
#if DEBUG
                Console.WriteLine(e.ToString());
#else
                Console.WriteLine(e.Message);
#endif
            }
        }

        Console.WriteLine("Finished.");
    }
}
