using Pannella.Models;
using Pannella.Models.Archive;
using Pannella.Services;

namespace Pannella.Helpers;

public static class GlobalHelper
{
    public static Archive ArchiveFiles { get; private set; }
    public static SettingsManager SettingsManager { get; private set ;}
    public static string UpdateDirectory { get; private set; }
    public static string[] Blacklist { get; private set; }
    public static List<Core> Cores { get; private set; }
    public static List<Core> InstalledCores { get; private set; }

    private static bool isInitialized;

    public static async void Initialize(string path)
    {
        if (!isInitialized)
        {
            isInitialized = true;
            UpdateDirectory = path;
            SettingsManager = new SettingsManager(path);
            Cores = await CoresService.GetCores();
            SettingsManager.InitializeCoreSettings(Cores);
            RefreshInstalledCores();
            Blacklist = await AssetsService.GetBlacklist();

            Console.WriteLine("Loading Assets Index...");

            if (SettingsManager.GetConfig().use_custom_archive)
            {
                var custom = SettingsManager.GetConfig().custom_archive;
                Uri baseUrl = new Uri(custom["url"]);
                Uri url = new Uri(baseUrl, custom["index"]);

                ArchiveFiles = await ArchiveService.GetFilesCustom(url.ToString());
            }
            else
            {
                ArchiveFiles = await ArchiveService.GetFiles(SettingsManager.GetConfig().archive_name);
            }

            RefreshInstalledCores();
        }
    }

    public static void ReloadSettings()
    {
        SettingsManager = new SettingsManager(UpdateDirectory, Cores);
    }

    public static void RefreshInstalledCores()
    {
        InstalledCores = Cores.Where(c => c.IsInstalled()).ToList();
    }

    public static Core GetCore(string identifier)
    {
        return Cores.Find(i => i.identifier == identifier);
    }
}
