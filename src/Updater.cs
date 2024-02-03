using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace pannella.analoguepocket;

public class PocketCoreUpdater : Base
{
    private const string FIRMWARE_FILENAME_PATTERN = "pocket_firmware_*.bin";
    private const string FIRMWARE_URL = "https://www.analogue.co/support/pocket/firmware/latest";
    private static readonly Regex BIN_REGEX = new Regex(@"(?inx)
        <a \s [^>]*
            href \s* = \s*
                (?<q> ['""] )
                    (?<url> [^'""]*\.bin )
                \k<q>
        [^>]* >");
    private bool _downloadAssets = false;
    private bool _preservePlatformsFolder = false;

    private string _githubApiKey = "";

    private bool _downloadFirmware = true;
    private bool _deleteSkippedCores = true;
    private bool _useConsole = false;
    private bool _renameJotegoCores = true;
    private bool _jtBeta = false;
    private bool _backupSaves = false;
    private string _backupSavesLocation;

    private Dictionary<string, string> _platformFiles = new Dictionary<string, string>();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="updateDirectory">The directory to install/update openFPGA cores in.</param>
    /// <param name="settingsPath">Path to settings json file</param>
    public PocketCoreUpdater(string updateDirectory, string? settingsPath = null)
    {
        Factory.GetGlobals().UpdateDirectory = updateDirectory;
        Directory.CreateDirectory(Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores"));

        if(settingsPath != null) {
            Factory.GetGlobals().SettingsPath = settingsPath;
        } else {
            Factory.GetGlobals().SettingsPath = updateDirectory;
        }
    }

    public async Task Initialize()
    {
        LoadSettings();
        await LoadPlatformFiles();
        await LoadCores();
        await RefreshInstalledCores();
        await LoadArchive();
        await LoadBlacklist();
    }

    private async Task RefreshInstalledCores()
    {
        var installedCores = new List<Core>();
        foreach(Core c in GlobalHelper.Instance.Cores) {
            if(c.isInstalled()) {
                installedCores.Add(c);
            }
        }
        GlobalHelper.Instance.InstalledCores = installedCores;
    }

    private async Task LoadPlatformFiles()
    {
        try {
            List<Github.File> files = await GithubApi.GetFiles("dyreschlock", "pocket-platform-images", "arcade/Platforms",
                GlobalHelper.Instance.SettingsManager.GetConfig().github_token);
            Dictionary<string, string> platformFiles = new Dictionary<string, string>();
            foreach(Github.File file in files) {
                string url = file.download_url;
                string filename = file.name;
                if (filename.EndsWith(".json")) {
                    string platform = Path.GetFileNameWithoutExtension(filename);
                    platformFiles.Add(platform, url);
                }
            }
            _platformFiles = platformFiles;
        } catch (Exception e) {
            _writeMessage("Unable to retrieve archive contents. Asset download may not work.");
            _platformFiles = new Dictionary<string, string>();
        }
    }

    private async Task LoadArchive()
    {
        _writeMessage("Loading Assets Index...");
        if(Factory.GetGlobals().SettingsManager.GetConfig().use_custom_archive) {
            var custom = Factory.GetGlobals().SettingsManager.GetConfig().custom_archive;
            Uri baseUrl = new Uri(custom["url"]);
            Uri url = new Uri(baseUrl, custom["index"]);
        
            Factory.GetGlobals().ArchiveFiles = await ArchiveService.GetFilesCustom(url.ToString());
        } else {
            Factory.GetGlobals().ArchiveFiles = await ArchiveService.GetFiles(Factory.GetGlobals().SettingsManager.GetConfig().archive_name);
        }
    }

    private async Task LoadBlacklist()
    {
        Factory.GetGlobals().Blacklist = await AssetsService.GetBlacklist();
    }

    public async Task LoadCores()
    {
        Factory.GetGlobals().Cores = await CoresService.GetCores();
        Factory.GetGlobals().SettingsManager.InitializeCoreSettings(Factory.GetGlobals().Cores);
        foreach(Core core in Factory.GetGlobals().Cores) {
            core.StatusUpdated += updater_StatusUpdated; //attach handler to bubble event up
        }
    }

    public List<Core> GetCores()
    {
        return Factory.GetGlobals().Cores;
    }

    public void LoadSettings()
    {
         Factory.GetGlobals().SettingsManager = new SettingsManager(Factory.GetGlobals().SettingsPath, Factory.GetGlobals().Cores);
    }

    public List<Core> GetMissingCores() => Factory.GetGlobals().SettingsManager?.GetMissingCores() ?? new List<Core>();

    /// <summary>
    /// Turn on/off printing progress messages to the console
    /// </summary>
    /// <param name="set">Set to true to turn on console messages</param>
    public void PrintToConsole(bool set)
    {
        _useConsole = set;
    }

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

    public void DownloadFirmware(bool set)
    {
        _downloadFirmware = set;
    }

    public void BackupSaves(bool set, string location)
    {
        _backupSaves = set;
        _backupSavesLocation = location;
    }

    //get api and local cores
    private async Task<List<Core>> getAllCores()
    {
        List<Core> cores = Factory.GetGlobals().Cores;
        List<Core> local = await GetLocalCores();
        foreach(Core core in local) {
            core.StatusUpdated += updater_StatusUpdated; //attach handler to bubble event up
        }
        cores.AddRange(local);

        return cores;
    }

    public async Task BuildInstanceJSON(bool overwrite = false, string? corename = null)
    {
        List<Core> cores = await getAllCores();
        foreach(Core core in Factory.GetGlobals().Cores) {
            if(core.CheckInstancePackager() && (corename == null || corename == core.identifier)) {
                _writeMessage(core.identifier);
                core.BuildInstanceJSONs(overwrite);
                Divide();
            }
        }
    }

    /// <summary>
    /// Run the full openFPGA core download and update process
    /// </summary>
    public async Task RunUpdates(string? id = null, bool clean = false)
    {
        List<Dictionary<string, string>> installed = new List<Dictionary<string, string>>();
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();
        Dictionary<string, Object> results = new Dictionary<string, Object>();
        string firmwareDownloaded = "";
        if(Factory.GetGlobals().Cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }

        if (_backupSaves)
        {
            AssetsService.BackupSaves(Factory.GetGlobals().UpdateDirectory, _backupSavesLocation);
        }
        
        if(_downloadFirmware && id == null) {
            firmwareDownloaded = await UpdateFirmware();
        }

        await ExtractBetaKey();

        List<Core> cores = await getAllCores();
        string json;
        foreach(Core core in Factory.GetGlobals().Cores) {
            if(id != null && core.identifier != id) {
                continue;
            }

            core.downloadAssets = (_downloadAssets && (id==null));
            core.buildInstances = (Factory.GetGlobals().SettingsManager.GetConfig().build_instance_jsons && (id==null));
            try {
                if(Factory.GetGlobals().SettingsManager.GetCoreSettings(core.identifier).skip) {
                    await DeleteCore(core);
                    continue;
                }

                if (core.requires_license && !_jtBeta) {
                    continue; //skip if you don't have the key
                }
                
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }

                _writeMessage("Checking Core: " + name);
                var mostRecentRelease = core.version;

                if(mostRecentRelease == null) {
                    _writeMessage("No releases found. Skipping");
                    await CopyBetaKey(core);
                    results = await core.DownloadAssets();
                    installedAssets.AddRange(results["installed"] as List<string>);
                    skippedAssets.AddRange(results["skipped"] as List<string>);
                    if((bool)results["missingBetaKey"]) {
                        missingBetaKeys.Add(core.identifier);
                    }
                    await JotegoRename(core);
                    Divide();
                    continue;
                }

                _writeMessage(mostRecentRelease + " is the most recent release, checking local core...");
                if (core.isInstalled()) {
                    Analogue.Cores.Core.Core localCore = core.getConfig();
                    string localVersion = localCore.metadata.version;
                    
                    if(localVersion != null) {
                        _writeMessage("local core found: " + localVersion);
                    }

                    if (mostRecentRelease != localVersion || clean){
                        _writeMessage("Updating core");
                    } else {
                        await CopyBetaKey(core);
                        results = await core.DownloadAssets();
                        await JotegoRename(core);
                        installedAssets.AddRange(results["installed"] as List<string>);
                        skippedAssets.AddRange(results["skipped"] as List<string>);
                        if((bool)results["missingBetaKey"]) {
                            missingBetaKeys.Add(core.identifier);
                        }
                        _writeMessage("Up to date. Skipping core");
                        Divide();
                        continue;
                    }
                } else {
                    _writeMessage("Downloading core");
                }
                
                if(await core.Install(clean)) {
                    Dictionary<string, string> summary = new Dictionary<string, string>();
                    summary.Add("version", mostRecentRelease);
                    summary.Add("core", core.identifier);
                    summary.Add("platform", core.platform.name);
                    installed.Add(summary);
                }
                await JotegoRename(core);
                await CopyBetaKey(core);
                results = await core.DownloadAssets();
                installedAssets.AddRange(results["installed"] as List<string>);
                skippedAssets.AddRange(results["skipped"] as List<string>);
                if((bool)results["missingBetaKey"]) {
                    missingBetaKeys.Add(core.identifier);
                }
                _writeMessage("Installation complete.");
                Divide();
                
            } catch(Exception e) {
                _writeMessage("Uh oh something went wrong.");
                _writeMessage(e.Message);
            }
        } 

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs();
        args.Message = "Update Process Complete";
        args.InstalledCores = installed;
        args.InstalledAssets = installedAssets;
        args.SkippedAssets = skippedAssets;
        args.MissingBetaKeys = missingBetaKeys;
        args.FirmwareUpdated = firmwareDownloaded;
        OnUpdateProcessComplete(args);
    }

    private async Task JotegoRename(Core core)
    {
        if(_renameJotegoCores && Factory.GetGlobals().SettingsManager.GetCoreSettings(core.identifier).platform_rename 
                && core.identifier.Contains("jotego")) {
            core.platform_id = core.identifier.Split('.')[1]; //whatever
            string path = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Platforms", core.platform_id + ".json");
            string json = File.ReadAllText(path);
            Dictionary<string, Platform> data = JsonSerializer.Deserialize<Dictionary<string,Platform>>(json);
            Platform platform = data["platform"];
            if(_platformFiles.ContainsKey(core.platform_id) && platform.name == core.platform_id) {
                _writeMessage("Updating JT Platform Name...");
                await Factory.GetHttpHelper().DownloadFileAsync(_platformFiles[core.platform_id], path);
                _writeMessage("Complete");
            }
        }
    }

    private async Task CopyBetaKey(Core core)
    {
        if(core.JTBetaCheck()) {
            Analogue.Cores.Core.Core info = core.getConfig();
            string path = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Assets", info.metadata.platform_ids[core.betaSlotPlatformIdIndex], "common");
            if(!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            string keyPath = Path.Combine(Factory.GetGlobals().UpdateDirectory, "betakeys");
            if(Directory.Exists(keyPath) && Directory.Exists(path)) {
                Util.CopyDirectory(keyPath, path, false, true);
                _writeMessage("Beta key copied to common directory.");
            }
        }
    }

    private async Task ExtractBetaKey()
    {
        string keyPath = Path.Combine(Factory.GetGlobals().UpdateDirectory, "betakeys");
        string file = Path.Combine(Factory.GetGlobals().UpdateDirectory, "jtbeta.zip");
        if(File.Exists(file)) {
            _jtBeta = true;
            _writeMessage("Extracting JT beta key...");
            ZipFile.ExtractToDirectory(file, keyPath, true);
        }
    }

    public async Task RunAssetDownloader(string? id = null)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();
        Dictionary<string, Object> results = new Dictionary<string, Object>();
        if(Factory.GetGlobals().Cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }
        List<Core> cores = await getAllCores();
        foreach(Core core in Factory.GetGlobals().Cores) {
            if(id != null && core.identifier != id) {
                continue;
            }

            if(Factory.GetGlobals().SettingsManager.GetCoreSettings(core.identifier).skip) {
                continue;
            }

            core.downloadAssets = true;
            try {
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }
                _writeMessage(core.identifier);
                results = await core.DownloadAssets();
                installedAssets.AddRange(results["installed"] as List<string>);
                skippedAssets.AddRange(results["skipped"] as List<string>);
                if((bool)results["missingBetaKey"]) {
                    missingBetaKeys.Add(core.identifier);
                }
                Divide();
            } catch(Exception e) {
                _writeMessage("Uh oh something went wrong.");
                _writeMessage(e.Message);
            }
        } 

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs();
        args.Message = "All Done";
        args.InstalledAssets = installedAssets;
        args.SkippedAssets = skippedAssets;
        args.MissingBetaKeys = missingBetaKeys;
        OnUpdateProcessComplete(args);
    }

    public async Task ForceDisplayModes(string? id = null)
    {
        if(Factory.GetGlobals().Cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }
        List<Core> cores = await getAllCores();
        foreach(Core core in Factory.GetGlobals().Cores) {
            if(id != null && core.identifier != id) {
                continue;
            }

            if(Factory.GetGlobals().SettingsManager.GetCoreSettings(core.identifier).skip) {
                continue;
            }

            core.downloadAssets = true;
            try {
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }
                _writeMessage("Updating " + core.identifier);
                await core.AddDisplayModes();
                Divide();
            } catch(Exception e) {
                _writeMessage("Uh oh something went wrong.");
                _writeMessage(e.Message);
            }
        } 
        _writeMessage("Finished.");
    }

    private void Divide()
    {
        _writeMessage("-------------");
    }

    public async Task<List<Core>> GetLocalCores()
    {
        string coresDirectory = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores");
        string[] directories = Directory.GetDirectories(coresDirectory,"*", SearchOption.TopDirectoryOnly);
        List<Core> all = new List<Core>();
        foreach(string name in directories) {
            string n = Path.GetFileName(name);
            var matches = Factory.GetGlobals().Cores.Where(i=>i.identifier == n);
            if(matches.Count<Core>() == 0) {
                Core c = new Core {
                    identifier = n
                };
                c.platform = c.ReadPlatformFile();
                all.Add(c);
            }
        }

        return all;
    }

    public void SetGithubApiKey(string key)
    {
        _githubApiKey = key;
    }

    protected virtual void OnUpdateProcessComplete(UpdateProcessCompleteEventArgs e)
    {
        EventHandler<UpdateProcessCompleteEventArgs> handler = UpdateProcessComplete;
        if(handler != null)
        {
            handler(this, e);
        }
        RefreshInstalledCores();
    }

    public async Task<string> UpdateFirmware()
    {
        string version = "";
        _writeMessage("Checking for firmware updates...");
        var details = await AnalogueFirmware.GetDetails();
        
        string[] parts = details.download_url.Split("/");
        string filename = parts[parts.Length-1];
        string filepath = Path.Combine(Factory.GetGlobals().UpdateDirectory, filename);
        if(!File.Exists(filepath) || !Util.CompareChecksum(filepath, details.md5, Util.HashTypes.MD5)) {
            version = filename;
            var oldfiles = Directory.GetFiles(Factory.GetGlobals().UpdateDirectory, FIRMWARE_FILENAME_PATTERN);
            _writeMessage("Firmware update found. Downloading...");
            await Factory.GetHttpHelper().DownloadFileAsync(details.download_url, Path.Combine(Factory.GetGlobals().UpdateDirectory, filename));
            _writeMessage("Download Complete");
            _writeMessage(Path.Combine(Factory.GetGlobals().UpdateDirectory, filename));
            foreach (string oldfile in oldfiles) {
                if (File.Exists(oldfile) && Path.GetFileName(oldfile) != filename) {
                    _writeMessage("Deleting old firmware file...");
                    File.Delete(oldfile);
                }
            }
            _writeMessage("To install firmware, restart your Pocket.");
        } else {
            _writeMessage("Firmware up to date.");
        }
        Divide();
        return version;
    }

    public void DeleteSkippedCores(bool value)
    {
        _deleteSkippedCores = value;
    }

    public async Task DeleteCore(Core core, bool force = false, bool nuke = false)
    {
        if(!_deleteSkippedCores || !force) {
            return;
        }

        core.Uninstall(nuke);
    }

    private void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }
    public event EventHandler<UpdateProcessCompleteEventArgs>? UpdateProcessComplete;

    public void SetDownloadProgressHandler(EventHandler<DownloadProgressEventArgs> handler)
    {
        Factory.GetHttpHelper().DownloadProgressUpdate += handler;
    }
}

public class UpdateProcessCompleteEventArgs : EventArgs
{
    /// <summary>
    /// Some kind of results
    /// </summary>
    public string Message { get; set; }
    public List<Dictionary<string, string>> InstalledCores { get; set; }
    public List<string> InstalledAssets { get; set; }
    public List<string> SkippedAssets { get; set; }
    public string FirmwareUpdated { get; set; } = "";
    public List<string> MissingBetaKeys { get; set; }
}
