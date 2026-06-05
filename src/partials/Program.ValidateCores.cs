using Pannella.Helpers;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    private static void ValidateCores(bool fix)
    {
        CoresService coresService = ServiceHelper.CoresService;
        string coresDirectory = Path.Combine(ServiceHelper.UpdateDirectory, "Cores");

        if (!Directory.Exists(coresDirectory))
        {
            Console.WriteLine("No Cores directory found. Nothing to validate.");
            return;
        }

        // Enumerate the Cores directory directly rather than going through
        // InstalledCores: building that list reads each core's JSON, so a single
        // corrupt core.json would throw before we could report it - which is
        // exactly what this command exists to find.
        List<string> coreIds = Directory
            .GetDirectories(coresDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(id => !string.IsNullOrEmpty(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"Validating {coreIds.Count} installed core(s)...");

        List<string> brokenCores = new List<string>();

        foreach (string id in coreIds)
        {
            List<string> problems = GetCoreJsonProblems(coresService, id);

            if (problems.Count == 0)
            {
                continue;
            }

            brokenCores.Add(id);
            Console.WriteLine($"  [INVALID] {id}");

            foreach (string problem in problems)
            {
                Console.WriteLine($"              - {problem}");
            }
        }

        if (brokenCores.Count == 0)
        {
            Console.WriteLine("All installed cores have valid JSON.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{brokenCores.Count} core(s) have missing or invalid JSON: {string.Join(", ", brokenCores)}");

        if (!fix)
        {
            Console.WriteLine("Run again with --fix to reinstall the affected cores.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Reinstalling affected cores...");

        // Remove the broken core directories first so the reinstall is a clean
        // fresh install. RunUpdates reads the existing core.json to determine the
        // local version before it would otherwise clean, so leaving a corrupt
        // core.json in place makes the reinstall throw instead of repairing it.
        // (Saves/Memories live outside Cores/, so this does not touch user data.)
        foreach (string id in brokenCores)
        {
            string coreDirectory = Path.Combine(coresDirectory, id);

            try
            {
                if (Directory.Exists(coreDirectory))
                {
                    Directory.Delete(coreDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not remove {id} before reinstall: {Util.GetExceptionMessage(ex)}");
            }
        }

        CoreUpdaterService coreUpdaterService = new CoreUpdaterService(
            ServiceHelper.UpdateDirectory,
            ServiceHelper.CoresService.Cores,
            ServiceHelper.FirmwareService,
            ServiceHelper.SettingsService,
            ServiceHelper.CoresService);

        coreUpdaterService.StatusUpdated += coreUpdater_StatusUpdated;
        coreUpdaterService.UpdateProcessComplete += coreUpdater_UpdateProcessComplete;

        coreUpdaterService.RunUpdates(brokenCores.ToArray(), clean: true);

        // Re-validate to confirm the repair actually worked.
        List<string> stillBroken = brokenCores
            .Where(id => GetCoreJsonProblems(coresService, id).Count > 0)
            .ToList();

        Console.WriteLine();

        if (stillBroken.Count == 0)
        {
            Console.WriteLine($"Repaired {brokenCores.Count} core(s).");
        }
        else
        {
            Console.WriteLine($"{stillBroken.Count} core(s) could not be repaired: {string.Join(", ", stillBroken)}");
            Console.WriteLine("(A core can only be repaired if it still exists in the cores inventory.)");
            Environment.ExitCode = 1;
        }
    }

    // Returns a list of human-readable problems with a core's JSON files.
    // An empty list means the core's JSON is valid.
    private static List<string> GetCoreJsonProblems(CoresService coresService, string identifier)
    {
        List<string> problems = new List<string>();

        // core.json is required and must parse.
        try
        {
            if (coresService.ReadCoreJson(identifier) == null)
            {
                problems.Add("core.json is missing");
            }
        }
        catch (Exception ex)
        {
            problems.Add($"core.json is invalid: {Util.GetExceptionMessage(ex)}");
        }

        // data.json and video.json are optional (null when absent) but must parse when present.
        try
        {
            coresService.ReadDataJson(identifier);
        }
        catch (Exception ex)
        {
            problems.Add($"data.json is invalid: {Util.GetExceptionMessage(ex)}");
        }

        try
        {
            coresService.ReadVideoJson(identifier);
        }
        catch (Exception ex)
        {
            problems.Add($"video.json is invalid: {Util.GetExceptionMessage(ex)}");
        }

        return problems;
    }
}
