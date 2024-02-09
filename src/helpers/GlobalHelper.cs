using Pannella.Models;
using Pannella.Models.Archive;
using Pannella.Services;

namespace Pannella.Helpers;

public static class GlobalHelper
{
    private static Archive archiveFiles;

    public static Archive ArchiveFiles
    {
        get
        {
            if (archiveFiles == null)
            {
                Console.WriteLine("Loading Assets Index...");

                if (SettingsManager.GetConfig().use_custom_archive)
                {
                    var custom = SettingsManager.GetConfig().custom_archive;
                    Uri baseUrl = new Uri(custom["url"]);
                    Uri url = new Uri(baseUrl, custom["index"]);

                    archiveFiles = ArchiveService.GetFilesCustom(url.ToString()).Result;
                }
                else
                {
                    archiveFiles = ArchiveService.GetFiles(SettingsManager.GetConfig().archive_name).Result;
                }
            }

            return archiveFiles;
        }
    }

    private static Archive gameAndWatchArchiveFiles;

    public static Archive GameAndWatchArchiveFiles
    {
        get
        {
            if (gameAndWatchArchiveFiles == null)
            {
                Console.WriteLine("Loading Game and Watch Assets Index...");

                string archiveName = SettingsManager.GetConfig().archive_name;
                string gnwArchiveName = SettingsManager.GetConfig().gnw_archive_name;

                if (gnwArchiveName != archiveName)
                {
                    gameAndWatchArchiveFiles = ArchiveService.GetFiles(gnwArchiveName).Result;

                    // remove the metadata files since we're processing the entire json list
                    gameAndWatchArchiveFiles.files.RemoveAll(file =>
                        Path.GetExtension(file.name) is ".sqlite" or ".torrent" or ".xml");
                }
                else
                {
                    // there are GNW files in the openFPGA-files archive as well as the archive maintained by Espiox
                    // if the GNW archive is set to the openFPGA-files archive, create a second archive
                    // with just the GNW files from it so things behave correctly
                    gameAndWatchArchiveFiles = new Archive
                    {
                        item_last_updated = ArchiveFiles.item_last_updated,
                        files = ArchiveFiles.files.Where(file => file.name.EndsWith(".gnw")).ToList()
                    };

                    gameAndWatchArchiveFiles.files_count = gameAndWatchArchiveFiles.files.Count;
                }
            }

            return gameAndWatchArchiveFiles;
        }
    }

    public static SettingsManager SettingsManager { get; private set ;}
    public static string UpdateDirectory { get; private set; }
    public static string[] Blacklist { get; private set; }
    public static List<Core> Cores { get; private set; }
    public static List<Core> InstalledCores { get; private set; }

    public static List<Core> InstalledCoresWithSponsors { get; private set; }

    private static bool isInitialized;

    public static async Task Initialize(string path)
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
        }
    }

    public static void ReloadSettings()
    {
        SettingsManager = new SettingsManager(UpdateDirectory, Cores);
    }

    public static void RefreshInstalledCores()
    {
        InstalledCores = Cores.Where(c => c.IsInstalled()).ToList();
        InstalledCoresWithSponsors = InstalledCores.Where(c => c.sponsor != null).ToList();
    }

    public static Core GetCore(string identifier)
    {
        return Cores.Find(i => i.identifier == identifier);
    }
}
