using Pannella.Helpers;

namespace Pannella;

internal static partial class Program
{
    private static bool ClearArchiveCache(bool promptForConfirmation)
    {
        if (!ServiceHelper.SettingsService.Config.cache_archive_files)
        {
            Console.WriteLine("Archive caching is not enabled.");
            return false;
        }

        string cacheDir = ServiceHelper.CacheDirectory;

        if (!Directory.Exists(cacheDir))
        {
            Console.WriteLine("Cache directory is already empty.");
            return false;
        }

        if (promptForConfirmation && !AskYesNoQuestion("Are you sure you want to clear the archive cache?"))
            return false;

        Directory.Delete(cacheDir, recursive: true);
        Console.WriteLine("Archive cache cleared.");
        return true;
    }
}
