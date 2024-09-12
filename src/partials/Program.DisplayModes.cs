using Pannella.Helpers;

namespace Pannella;

internal partial class Program
{
    private static void EnableDisplayModes(string[] displayModes = null, bool isCurated = false)
    {
        var cores = ServiceHelper.CoresService.Cores.Where(core =>
            !ServiceHelper.SettingsService.GetCoreSettings(core.identifier).skip).ToList();

        foreach (var core in cores)
        {
            if (core == null)
            {
                Console.WriteLine("Core name is required. Skipping");
                return;
            }

            try
            {
                // not sure if this check is still needed
                if (core.identifier == null)
                {
                    Console.WriteLine("Core Name is required. Skipping.");
                    continue;
                }

                Console.WriteLine("Updating " + core.identifier);
                ServiceHelper.CoresService.AddDisplayModes(core.identifier, displayModes, isCurated);
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
