using Pannella.Models.Events;
using Pannella.Services;

namespace Pannella.Helpers;

public static class ServiceHelper
{
    public static string UpdateDirectory { get; private set; } // move off this
    public static string SettingsDirectory { get; private set; } // for retrodriven's app
    public static string TempDirectory { get; private set; }
    public static CoresService CoresService { get; private set; }
    public static SettingsService SettingsService { get; private set ;}
    public static PlatformImagePacksService PlatformImagePacksService { get; private set; }
    public static FirmwareService FirmwareService { get; private set; }
    public static ArchiveService ArchiveService { get; private set; }
    public static AssetsService AssetsService { get; private set; }
    public static EventHandler<StatusUpdatedEventArgs> StatusUpdated { get; private set; }
    public static EventHandler<UpdateProcessCompleteEventArgs> UpdateProcessComplete { get; private set; }

    private static bool isInitialized;

    public static void Initialize(string path, string settingsPath, EventHandler<StatusUpdatedEventArgs> statusUpdated = null,
        EventHandler<UpdateProcessCompleteEventArgs> updateProcessComplete = null, bool forceReload = false)
    {
        if (!isInitialized || forceReload)
        {
            isInitialized = true;
            UpdateDirectory = path;
            SettingsDirectory = settingsPath;
            SettingsService = new SettingsService(settingsPath);
            ArchiveService = new ArchiveService(SettingsService.GetConfig().archives,
                SettingsService.GetConfig().crc_check, SettingsService.GetConfig().use_custom_archive);
            TempDirectory = SettingsService.GetConfig().temp_directory ?? UpdateDirectory;
            AssetsService = new AssetsService(SettingsService.GetConfig().use_local_blacklist);
            CoresService = new CoresService(path, SettingsService, ArchiveService, AssetsService);
            SettingsService.InitializeCoreSettings(CoresService.Cores);
            SettingsService.Save();
            PlatformImagePacksService = new PlatformImagePacksService(path, SettingsService.GetConfig().github_token,
                SettingsService.GetConfig().use_local_image_packs);
            FirmwareService = new FirmwareService();

            if (statusUpdated != null)
            {
                PlatformImagePacksService.StatusUpdated += statusUpdated;
                FirmwareService.StatusUpdated += statusUpdated;
                CoresService.StatusUpdated += statusUpdated;
                ArchiveService.StatusUpdated += statusUpdated;
                StatusUpdated = statusUpdated;
            }

            if (updateProcessComplete != null)
            {
                CoresService.UpdateProcessComplete += updateProcessComplete;
                UpdateProcessComplete = updateProcessComplete;
            }
        }
    }

    public static void ReloadSettings()
    {
        SettingsService = new SettingsService(SettingsDirectory, CoresService.Cores);
        // reload the archive service, in case that setting has changed
        ArchiveService = new ArchiveService(SettingsService.GetConfig().archives,
            SettingsService.GetConfig().crc_check, SettingsService.GetConfig().use_custom_archive);
        CoresService = new CoresService(UpdateDirectory, SettingsService, ArchiveService, AssetsService);
        CoresService.StatusUpdated += StatusUpdated;
    }
}
