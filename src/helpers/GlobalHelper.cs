using Pannella.Models;
using Pannella.Models.Archive;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Services;

namespace Pannella.Helpers;

public static class GlobalHelper
{
    public static SettingsManager SettingsManager { get; private set ;}
    public static string UpdateDirectory { get; private set; }
    public static List<Core> Cores { get; private set; }
    public static List<Core> InstalledCores { get; private set; }
    public static List<Core> InstalledCoresWithSponsors { get; private set; }
    public static PocketExtrasService PocketExtrasService { get; private set; }
    public static PlatformImagePacksService PlatformImagePacksService { get; private set; }
    public static FirmwareService FirmwareService { get; private set; }
    public static CoresService CoresService { get; private set; }
    public static JotegoService JotegoService { get; private set; }
    public static ArchiveService ArchiveService { get; private set; }

    private static bool isInitialized;

    public static void Initialize(string path, EventHandler<StatusUpdatedEventArgs> statusUpdated = null,
        EventHandler<UpdateProcessCompleteEventArgs> updateProcessComplete = null)
    {
        if (!isInitialized)
        {
            isInitialized = true;
            UpdateDirectory = path;
            SettingsManager = new SettingsManager(path);
            Cores = CoresService.GetOpenFpgaCoresInventory(); // should move this up before settings manager and pass into constructor
            RefreshLocalCores();
            PocketExtrasService = new PocketExtrasService(SettingsManager.GetConfig().github_token);
            PlatformImagePacksService = new PlatformImagePacksService();
            FirmwareService = new FirmwareService();
            CoresService = new CoresService();
            JotegoService = new JotegoService(SettingsManager.GetConfig().github_token);

            if (SettingsManager.GetConfig().use_custom_archive)
            {
                ArchiveService = new ArchiveService(SettingsManager.GetConfig().custom_archive,
                    SettingsManager.GetConfig().gnw_archive_name);
            }
            else
            {
                ArchiveService = new ArchiveService(SettingsManager.GetConfig().archive_name,
                    SettingsManager.GetConfig().gnw_archive_name);
            }

            if (statusUpdated != null)
            {
                PocketExtrasService.StatusUpdated += statusUpdated;
                PlatformImagePacksService.StatusUpdated += statusUpdated;
                FirmwareService.StatusUpdated += statusUpdated;
                CoresService.StatusUpdated += statusUpdated;
                JotegoService.StatusUpdated += statusUpdated;
            }

            if (updateProcessComplete != null)
            {
                PocketExtrasService.UpdateProcessComplete += updateProcessComplete;
            }
        }
    }

    private static IEnumerable<Core> GetLocalCores()
    {
        string coresDirectory = Path.Combine(UpdateDirectory, "Cores");

        // Create if it doesn't exist. -- Should we do this?
        // Stops error from being thrown if we do.
        Directory.CreateDirectory(coresDirectory);

        string[] directories = Directory.GetDirectories(coresDirectory, "*", SearchOption.TopDirectoryOnly);
        List<Core> all = new List<Core>();

        foreach (string name in directories)
        {
            string n = Path.GetFileName(name);
            var matches = Cores.Where(i => i.identifier == n);

            if (!matches.Any())
            {
                Core c = new Core { identifier = n };
                c.platform = c.ReadPlatformFile();
                all.Add(c);
            }
        }

        return all;
    }

    public static void ReloadSettings()
    {
        SettingsManager = new SettingsManager(UpdateDirectory, Cores);
    }

    public static void RefreshLocalCores()
    {
        Cores.AddRange(GetLocalCores());
        SettingsManager.InitializeCoreSettings(Cores); // this doesn't add new cores to the list
        RefreshInstalledCores();
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

    public static Core GetInstalledCore(string identifier)
    {
        return InstalledCores.Find(i => i.identifier == identifier);
    }
}
