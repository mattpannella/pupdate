using Pannella.Models;
using Pannella.Services;

namespace Pannella.Helpers;

public static class ServiceHelper
{
    public static string UpdateDirectory { get; private set; }
    public static CoresService CoresService { get; private set; }
    public static SettingsService SettingsService { get; private set ;}
    public static PocketExtrasService PocketExtrasService { get; private set; }
    public static PlatformImagePacksService PlatformImagePacksService { get; private set; }
    public static FirmwareService FirmwareService { get; private set; }
    public static JotegoService JotegoService { get; private set; }
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
            CoresService = new CoresService(path);
            SettingsService.InitializeCoreSettings(CoresService.Cores);
            PocketExtrasService = new PocketExtrasService(CoresService, SettingsService);
            PlatformImagePacksService = new PlatformImagePacksService(path, SettingsService.GetConfig().github_token,
                SettingsService.GetConfig().use_local_image_packs);
            FirmwareService = new FirmwareService();
            JotegoService = new JotegoService(path, SettingsService.GetConfig().github_token);
            AssetsService = new AssetsService(SettingsService.GetConfig().use_local_blacklist);

            if (SettingsService.GetConfig().use_custom_archive)
            {
                ArchiveService = new ArchiveService(SettingsService.GetConfig().custom_archive,
                    SettingsService.GetConfig().gnw_archive_name, SettingsService.GetConfig().crc_check);
            }
            else
            {
                ArchiveService = new ArchiveService(SettingsService.GetConfig().archive_name,
                    SettingsService.GetConfig().gnw_archive_name, SettingsService.GetConfig().crc_check);
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

    public static void ReloadSettings()
    {
        SettingsService = new SettingsService(UpdateDirectory, CoresService.Cores);
    }
}
