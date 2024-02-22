using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Extras;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Services;
using File = System.IO.File;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public class PocketCoreUpdater : BaseProcess
{
    public string InstallPath { get; set; }
    public List<Core> Cores { get; set; }
    public FirmwareService FirmwareService { get; set; }
    public JotegoService JotegoService { get; set; }
    public PocketExtrasService PocketExtrasService { get; set; }
    public SettingsManager SettingsManager { get; set; }

    public PocketCoreUpdater(
        string path,
        List<Core> cores,
        FirmwareService firmwareService = null,
        JotegoService jotegoService = null,
        PocketExtrasService pocketExtrasService = null,
        SettingsManager settingsManager = null)
    {
        this.InstallPath = path;
        this.Cores = cores;
        this.FirmwareService = firmwareService;
        this.JotegoService = jotegoService;
        this.PocketExtrasService = pocketExtrasService;
        this.SettingsManager = settingsManager;

        Directory.CreateDirectory(Path.Combine(path, "Cores"));

        foreach (Core core in this.Cores)
        {
            core.ClearStatusUpdated();
            core.StatusUpdated += updater_StatusUpdated; // attach handler to bubble event up
        }
    }

    public void BuildInstanceJson(bool overwrite = false, string coreName = null)
    {
        foreach (Core core in this.Cores)
        {
            if (core.CheckInstancePackager() && (coreName == null || coreName == core.identifier))
            {
                WriteMessage(core.identifier);
                core.BuildInstanceJSONs(overwrite);
                Divide();
            }
        }
    }

    /// <summary>
    /// Run the full openFPGA core download and update process
    /// </summary>
    public void RunUpdates(string id = null, bool clean = false, bool skipOutro = false)
    {
        List<Dictionary<string, string>> installed = new List<Dictionary<string, string>>();
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();
        string firmwareDownloaded = null;

        if (this.SettingsManager.GetConfig().backup_saves)
        {
            AssetsService.BackupSaves(this.InstallPath, this.SettingsManager.GetConfig().backup_saves_location);
            AssetsService.BackupMemories(this.InstallPath, this.SettingsManager.GetConfig().backup_saves_location);
        }

        if (this.SettingsManager.GetConfig().download_firmware && id == null)
        {
            if (this.FirmwareService != null)
            {
                firmwareDownloaded = this.FirmwareService.UpdateFirmware(this.InstallPath);
            }
            else
            {
                WriteMessage("Firmware Service is missing.");
            }

            Divide();
        }

        bool jtBetaKeyExists = this.JotegoService.ExtractBetaKey();

        foreach (var core in this.Cores.Where(core => id == null || core.identifier == id))
        {
            core.download_assets = this.SettingsManager.GetConfig().download_assets && id == null;
            core.build_instances = this.SettingsManager.GetConfig().build_instance_jsons && id == null;

            var coreSettings = this.SettingsManager.GetCoreSettings(core.identifier);

            try
            {
                if (coreSettings.skip)
                {
                    DeleteCore(core);
                    continue;
                }

                if (core.requires_license && !jtBetaKeyExists)
                {
                    missingBetaKeys.Add(core.identifier);
                    continue; // skip if you don't have the key
                }

                string name = core.identifier;

                if (name == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage("Checking Core: " + name);
                PocketExtra pocketExtra = PocketExtrasService.GetPocketExtra(name);
                bool isPocketExtraCombinationPlatform = coreSettings.pocket_extras &&
                                                        pocketExtra != null &&
                                                        pocketExtra.type == PocketExtraType.combination_platform;
                string mostRecentRelease = isPocketExtraCombinationPlatform
                    ? this.PocketExtrasService.GetMostRecentRelease(pocketExtra)
                    : core.version;

                Dictionary<string, object> results;

                if (mostRecentRelease == null)
                {
                    WriteMessage("No releases found. Skipping");

                    if (core.JTBetaCheck())
                        this.JotegoService.CopyBetaKey(core);

                    results = core.DownloadAssets();
                    installedAssets.AddRange(results["installed"] as List<string>);
                    skippedAssets.AddRange(results["skipped"] as List<string>);

                    if ((bool)results["missingBetaKey"])
                    {
                        missingBetaKeys.Add(core.identifier);
                    }

                    JotegoRename(core);
                    Divide();
                    continue;
                }

                WriteMessage(mostRecentRelease + " is the most recent release, checking local core...");

                if (core.IsInstalled())
                {
                    AnalogueCore localCore = core.GetConfig();
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
                        if (core.JTBetaCheck())
                            this.JotegoService.CopyBetaKey(core);

                        results = core.DownloadAssets();
                        JotegoRename(core);

                        installedAssets.AddRange(results["installed"] as List<string>);
                        skippedAssets.AddRange(results["skipped"] as List<string>);

                        if ((bool)results["missingBetaKey"])
                        {
                            missingBetaKeys.Add(core.identifier);
                        }

                        WriteMessage("Up to date. Skipping core");
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
                    if (clean && core.IsInstalled())
                    {
                        core.Delete();
                    }

                    this.PocketExtrasService.GetPocketExtra(pocketExtra, this.InstallPath, false, false);

                    Dictionary<string, string> summary = new Dictionary<string, string>
                    {
                        { "version", mostRecentRelease },
                        { "core", core.identifier },
                        { "platform", core.platform.name }
                    };

                    installed.Add(summary);
                }
                else if (core.Install(this.SettingsManager.GetConfig().preserve_platforms_folder, clean))
                {
                    Dictionary<string, string> summary = new Dictionary<string, string>
                    {
                        { "version", mostRecentRelease },
                        { "core", core.identifier },
                        { "platform", core.platform.name }
                    };

                    installed.Add(summary);
                }

                JotegoRename(core);

                if (core.JTBetaCheck())
                    this.JotegoService.CopyBetaKey(core);

                results = core.DownloadAssets();
                installedAssets.AddRange(results["installed"] as List<string>);
                skippedAssets.AddRange(results["skipped"] as List<string>);

                if ((bool)results["missingBetaKey"])
                {
                    missingBetaKeys.Add(core.identifier);
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

        JotegoService.DeleteBetaKey();

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "Update Process Complete.",
            InstalledCores = installed,
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingBetaKeys = missingBetaKeys,
            FirmwareUpdated = firmwareDownloaded,
            SkipOutro = skipOutro,
        };

        OnUpdateProcessComplete(args);
    }

    private void JotegoRename(Core core)
    {
        if (this.SettingsManager.GetConfig().fix_jt_names &&
            this.SettingsManager.GetCoreSettings(core.identifier).platform_rename &&
            core.identifier.Contains("jotego"))
        {
            core.platform_id = core.identifier.Split('.')[1];

            string path = Path.Combine(this.InstallPath, "Platforms", core.platform_id + ".json");
            string json = File.ReadAllText(path);
            Dictionary<string, Platform> data = JsonSerializer.Deserialize<Dictionary<string, Platform>>(json);
            Platform platform = data["platform"];

            if (this.JotegoService.RenamedPlatformFiles.TryGetValue(core.platform_id, out string value) &&
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
        if (this.SettingsManager.GetConfig().delete_skipped_cores || force)
        {
            core.Uninstall(nuke);
        }
    }

    private void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }
}
