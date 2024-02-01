using System.IO.Compression;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Services;
using File = System.IO.File;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella;

public class PocketCoreUpdater : Base
{
    private const string FIRMWARE_FILENAME_PATTERN = "pocket_firmware_*.bin";

    private bool _downloadAssets;
    private bool _preservePlatformsFolder;
    private bool _downloadFirmware = true;
    private bool _deleteSkippedCores = true;
    private bool _renameJotegoCores = true;
    private bool _jtBeta;
    private bool _backupSaves;
    private string _backupSavesLocation;
    private Dictionary<string, string> _platformFiles = new();

    public PocketCoreUpdater()
    {
        Directory.CreateDirectory(Path.Combine(GlobalHelper.UpdateDirectory, "Cores"));

        foreach (Core core in GlobalHelper.Cores)
        {
            core.StatusUpdated += updater_StatusUpdated; // attach handler to bubble event up
        }
    }

    #region Settings

    /// <summary>
    /// Turn on/off renaming the Jotego cores
    /// </summary>
    /// <param name="set">Set to true to rename the Jotego cores</param>
    public void RenameJotegoCores(bool set)
    {
        _renameJotegoCores = set;
    }

    /// <summary>
    /// Turn on/off the automatic BIOS downloader
    /// </summary>
    /// <param name="set">Set to true to enable automatic BIOS downloading</param>
    public void DownloadAssets(bool set)
    {
        _downloadAssets = set;
    }

    /// <summary>
    /// Turn on/off preserving customizations to /Platforms
    /// </summary>
    /// <param name="set">Set to true to enable preserving custom /Platforms changes</param>
    public void PreservePlatformsFolder(bool set)
    {
        _preservePlatformsFolder = set;
    }

    /// <summary>
    /// Turn on/off downloading the Analogue Pocket firmware
    /// </summary>
    /// <param name="set">Set to true to download the latest Analogue Pocket firmware</param>
    public void DownloadFirmware(bool set)
    {
        _downloadFirmware = set;
    }

    /// <summary>
    /// Turn on/off compressing and backing up the /Saves directory
    /// </summary>
    /// <param name="set">Set to true to compress and backup the /Saves directory</param>
    /// <param name="location">The absolute path to the backup location</param>
    public void BackupSaves(bool set, string location)
    {
        _backupSaves = set;
        _backupSavesLocation = location;
    }

    /// <summary>
    /// Turn on/off the deletion of skipped cores
    /// </summary>
    /// <param name="set">Set to true to delete the skipped cores</param>
    public void DeleteSkippedCores(bool set)
    {
        _deleteSkippedCores = set;
    }

    #endregion

    public async Task BuildInstanceJson(bool overwrite = false, string coreName = null)
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
    public async Task RunUpdates(string id = null, bool clean = false)
    {
        List<Dictionary<string, string>> installed = new List<Dictionary<string, string>>();
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();
        Dictionary<string, object> results = new Dictionary<string, object>();
        string firmwareDownloaded = string.Empty;

        if (GlobalHelper.Cores == null)
        {
            throw new Exception("Must initialize updater before running update process");
        }

        if (_backupSaves)
        {
            AssetsService.BackupSaves(GlobalHelper.UpdateDirectory, _backupSavesLocation);
        }

        if (_downloadFirmware && id == null)
        {
            firmwareDownloaded = await UpdateFirmware();
        }

        await ExtractBetaKey();

        foreach (var core in GlobalHelper.Cores.Where(core => id == null || core.identifier == id))
        {
            core.download_assets = _downloadAssets && id == null;
            core.build_instances = GlobalHelper.SettingsManager.GetConfig().build_instance_jsons && id == null;

            try
            {
                if (GlobalHelper.SettingsManager.GetCoreSettings(core.identifier).skip)
                {
                    await DeleteCore(core);
                    continue;
                }

                if (core.requires_license && !_jtBeta)
                {
                    continue; //skip if you don't have the key
                }

                string name = core.identifier;

                if (name == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage("Checking Core: " + name);
                var mostRecentRelease = core.version;

                await core.ReplaceCheck();

                if (mostRecentRelease == null)
                {
                    WriteMessage("No releases found. Skipping");

                    await CopyBetaKey(core);

                    results = await core.DownloadAssets();
                    installedAssets.AddRange(results["installed"] as List<string>);
                    skippedAssets.AddRange(results["skipped"] as List<string>);

                    if ((bool)results["missingBetaKey"])
                    {
                        missingBetaKeys.Add(core.identifier);
                    }

                    await JotegoRename(core);
                    Divide();
                    continue;
                }

                WriteMessage(mostRecentRelease + " is the most recent release, checking local core...");

                if (core.IsInstalled())
                {
                    AnalogueCore localCore = core.GetConfig();
                    string localVersion = localCore.metadata.version;

                    if (localVersion != null)
                    {
                        WriteMessage("local core found: " + localVersion);
                    }

                    if (mostRecentRelease != localVersion || clean)
                    {
                        WriteMessage("Updating core");
                    }
                    else
                    {
                        await CopyBetaKey(core);
                        results = await core.DownloadAssets();
                        await JotegoRename(core);

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
                    WriteMessage("Downloading core");
                }

                if (await core.Install(_preservePlatformsFolder, clean))
                {
                    Dictionary<string, string> summary = new Dictionary<string, string>
                    {
                        { "version", mostRecentRelease },
                        { "core", core.identifier },
                        { "platform", core.platform.name }
                    };

                    installed.Add(summary);
                }

                await JotegoRename(core);
                await CopyBetaKey(core);

                results = await core.DownloadAssets();
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
                WriteMessage(e.Message);
            }
        }

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "Update Process Complete",
            InstalledCores = installed,
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingBetaKeys = missingBetaKeys,
            FirmwareUpdated = firmwareDownloaded
        };

        OnUpdateProcessComplete(args);
    }

    private async Task LoadPlatformFiles()
    {
        try
        {
            List<GithubFile> files = await GithubApiService.GetFiles(
                "dyreschlock",
                "pocket-platform-images",
                "arcade/Platforms");
            Dictionary<string, string> platformFiles = new();

            foreach (GithubFile file in files)
            {
                string url = file.download_url;
                string filename = file.name;

                if (filename.EndsWith(".json"))
                {
                    string platform = Path.GetFileNameWithoutExtension(filename);

                    platformFiles.Add(platform, url);
                }
            }

            _platformFiles = platformFiles;
        }
        catch (Exception)
        {
            WriteMessage("Unable to retrieve archive contents. Asset download may not work.");
            _platformFiles = new Dictionary<string, string>();
        }
    }

    private async Task JotegoRename(Core core)
    {
        if (_renameJotegoCores &&
            GlobalHelper.SettingsManager.GetCoreSettings(core.identifier).platform_rename &&
            core.identifier.Contains("jotego"))
        {
            await LoadPlatformFiles();

            core.platform_id = core.identifier.Split('.')[1]; //whatever

            string path = Path.Combine(GlobalHelper.UpdateDirectory, "Platforms", core.platform_id + ".json");
            string json = File.ReadAllText(path);
            Dictionary<string, Platform> data = JsonSerializer.Deserialize<Dictionary<string, Platform>>(json);
            Platform platform = data["platform"];

            if (_platformFiles.TryGetValue(core.platform_id, out string value) && platform.name == core.platform_id)
            {
                WriteMessage("Updating JT Platform Name...");
                await HttpHelper.Instance.DownloadFileAsync(value, path);
                WriteMessage("Complete");
            }
        }
    }

    private async Task CopyBetaKey(Core core)
    {
        if (core.JTBetaCheck())
        {
            AnalogueCore info = core.GetConfig();
            string path = Path.Combine(
                GlobalHelper.UpdateDirectory,
                "Assets",
                info.metadata.platform_ids[core.beta_slot_platform_id_index],
                "common");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            string keyPath = Path.Combine(GlobalHelper.UpdateDirectory, "betakeys");

            if (Directory.Exists(keyPath) && Directory.Exists(path))
            {
                Util.CopyDirectory(keyPath, path, false, true);
                WriteMessage("Beta key copied to common directory.");
            }
        }
    }

    private async Task ExtractBetaKey()
    {
        string keyPath = Path.Combine(GlobalHelper.UpdateDirectory, "betakeys");
        string file = Path.Combine(GlobalHelper.UpdateDirectory, "jtbeta.zip");

        if (File.Exists(file))
        {
            _jtBeta = true;
            WriteMessage("Extracting JT beta key...");
            ZipFile.ExtractToDirectory(file, keyPath, true);
        }
    }

    public async Task RunAssetDownloader(string id = null)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();

        if (GlobalHelper.Cores == null)
        {
            throw new Exception("Must initialize updater before running update process");
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

                var results = await core.DownloadAssets();

                installedAssets.AddRange(results["installed"] as List<string>);
                skippedAssets.AddRange(results["skipped"] as List<string>);

                if ((bool)results["missingBetaKey"])
                {
                    missingBetaKeys.Add(core.identifier);
                }

                Divide();
            }
            catch (Exception e)
            {
                WriteMessage("Uh oh something went wrong.");
                WriteMessage(e.Message);
            }
        }

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "All Done",
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingBetaKeys = missingBetaKeys
        };

        OnUpdateProcessComplete(args);
    }

    public async Task ForceDisplayModes(string id = null)
    {
        if (GlobalHelper.Cores == null)
        {
            throw new Exception("Must initialize updater before running update process");
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

                WriteMessage("Updating " + core.identifier);
                await core.AddDisplayModes();
                Divide();
            }
            catch (Exception e)
            {
                WriteMessage("Uh oh something went wrong.");
                WriteMessage(e.Message);
            }
        }

        WriteMessage("Finished.");
    }

    private void OnUpdateProcessComplete(UpdateProcessCompleteEventArgs e)
    {
        EventHandler<UpdateProcessCompleteEventArgs> handler = UpdateProcessComplete;

        handler?.Invoke(this, e);

        GlobalHelper.RefreshInstalledCores();
    }

    public async Task<string> UpdateFirmware()
    {
        string version = "";
        WriteMessage("Checking for firmware updates...");
        var details = await AnalogueFirmwareService.GetDetails();

        string[] parts = details.download_url.Split("/");
        string filename = parts[parts.Length - 1];
        string filepath = Path.Combine(GlobalHelper.UpdateDirectory, filename);
        if (!File.Exists(filepath) || !Util.CompareChecksum(filepath, details.md5, Util.HashTypes.MD5))
        {
            version = filename;
            var oldfiles = Directory.GetFiles(GlobalHelper.UpdateDirectory, FIRMWARE_FILENAME_PATTERN);
            WriteMessage("Firmware update found. Downloading...");
            await HttpHelper.Instance.DownloadFileAsync(details.download_url,
                Path.Combine(GlobalHelper.UpdateDirectory, filename));
            WriteMessage("Download Complete");
            WriteMessage(Path.Combine(GlobalHelper.UpdateDirectory, filename));
            foreach (string oldfile in oldfiles)
            {
                if (File.Exists(oldfile) && Path.GetFileName(oldfile) != filename)
                {
                    WriteMessage("Deleting old firmware file...");
                    File.Delete(oldfile);
                }
            }

            WriteMessage("To install firmware, restart your Pocket.");
        }
        else
        {
            WriteMessage("Firmware up to date.");
        }

        Divide();
        return version;
    }

    public async Task DeleteCore(Core core, bool force = false, bool nuke = false)
    {
        if (!_deleteSkippedCores || !force)
        {
            return;
        }

        core.Uninstall(nuke);
    }

    private void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }

    public event EventHandler<UpdateProcessCompleteEventArgs> UpdateProcessComplete;

    public void SetDownloadProgressHandler(EventHandler<DownloadProgressEventArgs> handler)
    {
        HttpHelper.Instance.DownloadProgressUpdate += handler;
    }
}

public class UpdateProcessCompleteEventArgs : EventArgs
{
    public string Message { get; set; }
    public List<Dictionary<string, string>> InstalledCores { get; set; }
    public List<string> InstalledAssets { get; set; }
    public List<string> SkippedAssets { get; set; }
    public string FirmwareUpdated { get; set; } = string.Empty;
    public List<string> MissingBetaKeys { get; set; }
}
