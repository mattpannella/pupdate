using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Events;
using Pannella.Models.Extras;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using File = System.IO.File;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella.Services;

public class CoreUpdaterService : BaseProcess
{
    private readonly string installPath;
    private readonly List<Core> cores;
    private readonly FirmwareService firmwareService;
    private SettingsService settingsService;
    private CoresService coresService;

    public CoreUpdaterService(
        string path,
        List<Core> cores,
        FirmwareService firmwareService,
        SettingsService settingsService,
        CoresService coresService)
    {
        this.installPath = path;
        this.cores = cores;
        this.firmwareService = firmwareService;
        this.settingsService = settingsService;
        this.coresService = coresService;

        Directory.CreateDirectory(Path.Combine(path, "Cores"));
    }

    public void BuildInstanceJson(bool overwrite = false, string coreName = null)
    {
        foreach (Core core in this.cores)
        {
            if (this.coresService.CheckInstancePackager(core.identifier) && (coreName == null || coreName == core.identifier))
            {
                WriteMessage(core.identifier);
                this.coresService.BuildInstanceJson(core.identifier, overwrite);
                Divide();
            }
        }
    }

    /// <summary>
    /// Run the full openFPGA core download and update process
    /// </summary>
    public void RunUpdates(string[] ids = null, bool clean = false)
    {
        List<Dictionary<string, string>> installed = new List<Dictionary<string, string>>();
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingLicenses = new List<string>();
        string firmwareDownloaded = null;

        if (this.settingsService.GetConfig().backup_saves)
        {
            AssetsService.BackupSaves(this.installPath, this.settingsService.GetConfig().backup_saves_location);
            AssetsService.BackupMemories(this.installPath, this.settingsService.GetConfig().backup_saves_location);
        }

        if (this.settingsService.GetConfig().download_firmware && ids == null)
        {
            if (this.firmwareService != null)
            {
                firmwareDownloaded = this.firmwareService.UpdateFirmware(this.installPath);
            }
            else
            {
                WriteMessage("Firmware Service is missing.");
            }

            Divide();
        }

        this.coresService.RetrieveKeys();

        foreach (var core in this.cores.Where(core => ids == null || ids.Any(id => id == core.identifier)))
        {
            var coreSettings = this.settingsService.GetCoreSettings(core.identifier);

            try
            {
                if (coreSettings.skip)
                {
                    DeleteCore(core);
                    continue;
                }

                var requiresLicense = this.coresService.RequiresLicense(core.identifier);
                core.license_slot_filename = requiresLicense.Item4;
                core.license_slot_id = requiresLicense.Item2;
                core.license_slot_platform_id_index = requiresLicense.Item3;

                if (core.requires_license && !this.coresService.GrossCheck(core))
                {
                    missingLicenses.Add(core.identifier);
                    continue; // skip if you don't have the key
                }

                if (core.identifier == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage("Checking Core: " + core.identifier);

                PocketExtra pocketExtra = this.coresService.GetPocketExtra(core.identifier);
                bool isPocketExtraCombinationPlatform = coreSettings.pocket_extras &&
                                                        pocketExtra is { type: PocketExtraType.combination_platform };
                string mostRecentRelease;

                if (core.version == null && coreSettings.pocket_extras)
                {
                    mostRecentRelease = this.coresService.GetMostRecentRelease(pocketExtra);
                }
                else
                {
                    mostRecentRelease = core.version;
                    pocketExtra = null;
                }

                Dictionary<string, object> results;

                if (mostRecentRelease == null && pocketExtra == null && !coreSettings.pocket_extras)
                {
                    WriteMessage("No releases found. Skipping.");

                    if (requiresLicense.Item1)
                    {
                        this.coresService.CopyLicense(core);
                    }

                    results = this.coresService.DownloadAssets(core);
                    installedAssets.AddRange(results["installed"] as List<string>);
                    skippedAssets.AddRange(results["skipped"] as List<string>);

                    if ((bool)results["missingLicense"])
                    {
                        missingLicenses.Add(core.identifier);
                    }

                    JotegoRename(core);
                    Divide();
                    continue;
                }

                WriteMessage(mostRecentRelease + " is the most recent release, checking local core...");

                if (this.coresService.IsInstalled(core.identifier))
                {
                    AnalogueCore localCore = this.coresService.ReadCoreJson(core.identifier);
                    string localVersion = isPocketExtraCombinationPlatform
                        ? coreSettings.pocket_extras_version
                        : localCore.metadata.version;

                    if (localVersion != null)
                    {
                        WriteMessage("Local core found: " + localVersion);
                    }

                    if (mostRecentRelease != localVersion || clean)
                    {
                        WriteMessage("Updating core...");
                    }
                    else
                    {
                        if (requiresLicense.Item1)
                        {
                            this.coresService.CopyLicense(core);
                        }

                        if (coreSettings.pocket_extras &&
                            pocketExtra != null &&
                            pocketExtra.type != PocketExtraType.combination_platform)
                        {
                            WriteMessage("Pocket Extras found: " + coreSettings.pocket_extras_version);
                            var version = this.coresService.GetMostRecentRelease(pocketExtra);
                            WriteMessage(version + " is the most recent release...");

                            if (coreSettings.pocket_extras_version != version)
                            {
                                WriteMessage("Updating Pocket Extras...");
                                this.coresService.GetPocketExtra(pocketExtra, this.installPath, false);
                            }
                            else
                            {
                                WriteMessage("Up to date. Skipping Pocket Extras.");
                            }
                        }

                        results = this.coresService.DownloadAssets(core);

                        if (!coreSettings.pocket_extras)
                        {
                            JotegoRename(core);
                        }

                        installedAssets.AddRange(results["installed"] as List<string>);
                        skippedAssets.AddRange(results["skipped"] as List<string>);

                        if ((bool)results["missingLicense"])
                        {
                            missingLicenses.Add(core.identifier);
                        }

                        WriteMessage("Up to date. Skipping core.");
                        Divide();
                        continue;
                    }
                }
                else
                {
                    WriteMessage("Downloading core...");
                }

                if (isPocketExtraCombinationPlatform)
                {
                    if (clean && this.coresService.IsInstalled(core.identifier))
                    {
                        this.coresService.Delete(core.identifier, core.platform_id);
                    }

                    this.coresService.GetPocketExtra(pocketExtra, this.installPath, false);

                    Dictionary<string, string> summary = new Dictionary<string, string>
                    {
                        { "version", mostRecentRelease },
                        { "core", core.identifier },
                        { "platform", core.platform.name }
                    };

                    installed.Add(summary);
                }
                else if (this.coresService.Install(core, clean))
                {
                    Dictionary<string, string> summary = new Dictionary<string, string>
                    {
                        { "version", mostRecentRelease },
                        { "core", core.identifier },
                        { "platform", core.platform.name }
                    };

                    installed.Add(summary);
                }
                else if (coreSettings.pocket_extras &&
                         pocketExtra != null &&
                         pocketExtra.type != PocketExtraType.combination_platform)
                {
                    WriteMessage("Pocket Extras found: " + coreSettings.pocket_extras_version);
                    var version = this.coresService.GetMostRecentRelease(pocketExtra);
                    WriteMessage(version + " is the most recent release...");

                    if (coreSettings.pocket_extras_version != version)
                    {
                        WriteMessage("Updating Pocket Extras...");
                        this.coresService.GetPocketExtra(pocketExtra, this.installPath, false);
                    }
                    else
                    {
                        WriteMessage("Up to date. Skipping Pocket Extras.");
                    }
                }

                JotegoRename(core);

                var isJtBetaCore = this.coresService.RequiresLicense(core.identifier);

                if (isJtBetaCore.Item1)
                {
                    core.license_slot_id = isJtBetaCore.Item2;
                    core.license_slot_platform_id_index = isJtBetaCore.Item3;
                    core.license_slot_filename = isJtBetaCore.Item4;
                    this.coresService.CopyLicense(core);
                }

                results = this.coresService.DownloadAssets(core);
                installedAssets.AddRange(results["installed"] as List<string>);
                skippedAssets.AddRange(results["skipped"] as List<string>);

                if ((bool)results["missingLicense"])
                {
                    missingLicenses.Add(core.identifier);
                }

                WriteMessage("Installation complete.");
                Divide();
            }
            catch (Exception e)
            {
                WriteMessage("Uh oh something went wrong.");
#if DEBUG
                WriteMessage(e.ToString());
#else
                WriteMessage(e.Message);
#endif
            }
        }

        this.coresService.RefreshLocalCores();
        this.coresService.RefreshInstalledCores();

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "Update Process Complete.",
            InstalledCores = installed,
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingLicenses = missingLicenses,
            FirmwareUpdated = firmwareDownloaded,
            SkipOutro = false,
        };

        OnUpdateProcessComplete(args);
    }

    private void JotegoRename(Core core)
    {
        if (this.settingsService.GetConfig().fix_jt_names &&
            this.settingsService.GetCoreSettings(core.identifier).platform_rename &&
            core.identifier.Contains("jotego"))
        {
            core.platform_id = core.identifier.Split('.')[1];

            string path = Path.Combine(this.installPath, "Platforms", core.platform_id + ".json");
            string json = File.ReadAllText(path);
            Dictionary<string, Platform> data = JsonConvert.DeserializeObject<Dictionary<string, Platform>>(json);
            Platform platform = data["platform"];

            if (this.coresService.RenamedPlatformFiles.TryGetValue(core.platform_id, out string value) &&
                platform.name == core.platform_id)
            {
                WriteMessage("Updating JT Platform Name...");
                HttpHelper.Instance.DownloadFile(value, path);
                WriteMessage("Complete");
            }
        }
    }

    public void DeleteCore(Core core, bool force = false, bool nuke = false)
    {
        // If the core was a pocket extra or local the core inventory won't have it's platform id.
        // Load it from the core.json file if it's missing.
        if (string.IsNullOrEmpty(core.platform_id))
        {
            var analogueCore = this.coresService.ReadCoreJson(core.identifier);

            core.platform_id = analogueCore?.metadata.platform_ids[0];
        }

        // If the platform id is still missing, it's a pocket extra that was already deleted, so skip it.
        if (!string.IsNullOrEmpty(core.platform_id) &&
            (this.settingsService.GetConfig().delete_skipped_cores || force))
        {
            this.coresService.Uninstall(core.identifier, core.platform_id, nuke);
        }
    }

    public void ReloadSettings()
    {
        this.settingsService = ServiceHelper.SettingsService;
        this.coresService = ServiceHelper.CoresService;
    }
}
