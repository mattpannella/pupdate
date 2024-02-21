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
    private bool _downloadAssets;
    private bool _preservePlatformsFolder;
    private bool _downloadFirmware = true;
    private bool _deleteSkippedCores = true;
    private bool _renameJotegoCores = true;
    private bool _jtBeta;
    private bool _backupSaves;
    private string _backupSavesLocation;

    public PocketCoreUpdater(
        bool? renameJotegoCores = null,
        bool? downloadAssets = null,
        bool? preservePlatformsFolder = null,
        bool? downloadFirmware = null,
        bool? backupSaves = null,
        string backupSavesLocation = null,
        bool? deleteSkippedCores = null)
    {
        Directory.CreateDirectory(Path.Combine(GlobalHelper.UpdateDirectory, "Cores"));

        foreach (Core core in GlobalHelper.Cores)
        {
            core.ClearStatusUpdated();
            core.StatusUpdated += updater_StatusUpdated; // attach handler to bubble event up
        }

        this.UpdateSettings(renameJotegoCores, downloadAssets, preservePlatformsFolder, downloadFirmware, backupSaves,
            backupSavesLocation, deleteSkippedCores);
    }

    public void UpdateSettings(
        bool? renameJotegoCores = null,
        bool? downloadAssets = null,
        bool? preservePlatformsFolder = null,
        bool? downloadFirmware = null,
        bool? backupSaves = null,
        string backupSavesLocation = null,
        bool? deleteSkippedCores = null)
    {
        if (renameJotegoCores.HasValue)
            _renameJotegoCores = renameJotegoCores.Value;

        if (downloadAssets.HasValue)
            _downloadAssets = downloadAssets.Value;

        if (preservePlatformsFolder.HasValue)
            _preservePlatformsFolder = preservePlatformsFolder.Value;

        if (downloadFirmware.HasValue)
            _downloadFirmware = downloadFirmware.Value;

        if (backupSaves.HasValue)
            _backupSaves = backupSaves.Value;

        if (backupSavesLocation != null)
            _backupSavesLocation = backupSavesLocation;

        if (deleteSkippedCores.HasValue)
            _deleteSkippedCores = deleteSkippedCores.Value;
    }

    public void BuildInstanceJson(bool overwrite = false, string coreName = null)
    {
        foreach (Core core in GlobalHelper.Cores)
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
        string firmwareDownloaded = string.Empty;

        if (GlobalHelper.Cores == null)
        {
            throw new Exception("Must initialize updater before running update process.");
        }

        if (_backupSaves)
        {
            AssetsService.BackupSaves(GlobalHelper.UpdateDirectory, _backupSavesLocation);
            AssetsService.BackupMemories(GlobalHelper.UpdateDirectory, _backupSavesLocation);
        }

        if (_downloadFirmware && id == null)
        {
            firmwareDownloaded = GlobalHelper.FirmwareService.UpdateFirmware(GlobalHelper.UpdateDirectory);
            Divide();
        }

        _jtBeta = GlobalHelper.JotegoService.ExtractBetaKey();

        foreach (var core in GlobalHelper.Cores.Where(core => id == null || core.identifier == id))
        {
            core.download_assets = _downloadAssets && id == null;
            core.build_instances = GlobalHelper.SettingsManager.GetConfig().build_instance_jsons && id == null;

            var coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(core.identifier);

            try
            {
                if (coreSettings.skip)
                {
                    DeleteCore(core);
                    continue;
                }

                if (core.requires_license && !_jtBeta)
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
                string mostRecentRelease;
                PocketExtra pocketExtra = GlobalHelper.GetPocketExtra(name);
                bool isPocketExtraCombinationPlatform = coreSettings.pocket_extras &&
                                                        pocketExtra != null &&
                                                        pocketExtra.type == PocketExtraType.combination_platform;

                if (isPocketExtraCombinationPlatform)
                {
                    mostRecentRelease = GlobalHelper.PocketExtrasService.GetMostRecentRelease(pocketExtra);
                }
                else
                {
                    mostRecentRelease = core.version;
                }

                Dictionary<string, object> results;

                if (mostRecentRelease == null)
                {
                    WriteMessage("No releases found. Skipping");

                    if (core.JTBetaCheck())
                        GlobalHelper.JotegoService.CopyBetaKey(core);

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
                    string localVersion;

                    if (isPocketExtraCombinationPlatform)
                    {
                        localVersion = coreSettings.pocket_extras_version;
                    }
                    else
                    {
                        localVersion = localCore.metadata.version;
                    }

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
                            GlobalHelper.JotegoService.CopyBetaKey(core);

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

                    GlobalHelper.PocketExtrasService.GetPocketExtra(pocketExtra, GlobalHelper.UpdateDirectory,
                        false, false);

                    Dictionary<string, string> summary = new Dictionary<string, string>
                    {
                        { "version", mostRecentRelease },
                        { "core", core.identifier },
                        { "platform", core.platform.name }
                    };

                    installed.Add(summary);
                }
                else if (core.Install(_preservePlatformsFolder, clean))
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
                    GlobalHelper.JotegoService.CopyBetaKey(core);

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

        GlobalHelper.RefreshInstalledCores();
        OnUpdateProcessComplete(args);
    }

    private void JotegoRename(Core core)
    {
        if (_renameJotegoCores &&
            GlobalHelper.SettingsManager.GetCoreSettings(core.identifier).platform_rename &&
            core.identifier.Contains("jotego"))
        {
            core.platform_id = core.identifier.Split('.')[1]; //whatever

            string path = Path.Combine(GlobalHelper.UpdateDirectory, "Platforms", core.platform_id + ".json");
            string json = File.ReadAllText(path);
            Dictionary<string, Platform> data = JsonSerializer.Deserialize<Dictionary<string, Platform>>(json);
            Platform platform = data["platform"];

            if (GlobalHelper.JotegoRenamedPlatformFiles.TryGetValue(core.platform_id, out string value) &&
                platform.name == core.platform_id)
            {
                WriteMessage("Updating JT Platform Name...");
                HttpHelper.Instance.DownloadFile(value, path);
                WriteMessage("Complete");
            }
        }
    }

    public void RunAssetDownloader(string id = null, bool skipOutro = false)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();

        if (GlobalHelper.Cores == null)
        {
            throw new Exception("Must initialize updater before running update process.");
        }

        foreach (var core in GlobalHelper.Cores.Where(core => id == null || core.identifier == id)
                                               .Where(core => !GlobalHelper.SettingsManager.GetCoreSettings(core.identifier).skip))
        {
            core.download_assets = true;

            try
            {
                string name = core.identifier;

                if (name == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage(core.identifier);

                var results = core.DownloadAssets();

                installedAssets.AddRange((List<string>)results["installed"]);
                skippedAssets.AddRange((List<string>)results["skipped"]);

                if ((bool)results["missingBetaKey"])
                {
                    missingBetaKeys.Add(core.identifier);
                }

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

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "All Done",
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingBetaKeys = missingBetaKeys,
            SkipOutro = skipOutro,
        };

        OnUpdateProcessComplete(args);
    }

    public void DeleteCore(Core core, bool force = false, bool nuke = false)
    {
        if (_deleteSkippedCores || force)
        {
            core.Uninstall(nuke);
        }
    }

    private void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }
}
