using Pannella.Models;
using Pannella.Services;

namespace Pannella.Helpers;

public static class ServiceHelper
{
    public static string UpdateDirectory { get; private set; } // move off this
    public static CoresService CoresService { get; private set; }
    public static SettingsService SettingsService { get; private set ;}
    public static PlatformImagePacksService PlatformImagePacksService { get; private set; }
    public static FirmwareService FirmwareService { get; private set; }
    public static ArchiveService ArchiveService { get; private set; }
    public static AssetsService AssetsService { get; private set; }

    private static bool isInitialized;

    public static void Initialize(string path, EventHandler<StatusUpdatedEventArgs> statusUpdated = null,
        EventHandler<UpdateProcessCompleteEventArgs> updateProcessComplete = null)
    {
        if (!isInitialized)
        {
            isInitialized = true;
            UpdateDirectory = path;
            SettingsService = new SettingsService(path);
            ArchiveService = new ArchiveService(SettingsService.GetConfig().archives,
                SettingsService.GetConfig().crc_check, SettingsService.GetConfig().use_custom_archive);
            AssetsService = new AssetsService(SettingsService.GetConfig().use_local_blacklist);
            CoresService = new CoresService(path, SettingsService, ArchiveService, AssetsService);
            SettingsService.InitializeCoreSettings(CoresService.Cores);
            PlatformImagePacksService = new PlatformImagePacksService(path, SettingsService.GetConfig().github_token,
                SettingsService.GetConfig().use_local_image_packs);
            FirmwareService = new FirmwareService();

            if (statusUpdated != null)
            {
                PlatformImagePacksService.StatusUpdated += statusUpdated;
                FirmwareService.StatusUpdated += statusUpdated;
                CoresService.StatusUpdated += statusUpdated;
                ArchiveService.StatusUpdated += statusUpdated;
            }

            if (updateProcessComplete != null)
            {
                CoresService.UpdateProcessComplete += updateProcessComplete;
            }
        }
    }

    public static void ReloadSettings()
    {
        SettingsService = new SettingsService(UpdateDirectory, CoresService.Cores);
    }
}
