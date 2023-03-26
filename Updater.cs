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

    private bool _downloadFirmare = true;
    private bool _deleteSkippedCores = true;
    private bool _useConsole = false;
    private bool _renameJotegoCores = true;
    /// <summary>
    /// The directory where fpga cores will be installed and updated into
    /// </summary>
    public string UpdateDirectory { get; set; }

    public string SettingsPath { get; set; }

    private SettingsManager? _settingsManager;

    private List<Core>? _cores;

    private Dictionary<string, Dependency>? _assets;

    private archiveorg.Archive _archiveFiles;
    private string[] _blacklist;

    private Dictionary<string, string> _platformFiles = new Dictionary<string, string>();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="updateDirectory">The directory to install/update openFPGA cores in.</param>
    /// <param name="settingsPath">Path to settings json file</param>
    public PocketCoreUpdater(string updateDirectory, string? settingsPath = null)
    {
        UpdateDirectory = updateDirectory;
        Directory.CreateDirectory(Path.Combine(UpdateDirectory, "Cores"));

        if(settingsPath != null) {
            SettingsPath = settingsPath;
        } else {
            SettingsPath = updateDirectory;
        }
    }

    public async Task Initialize()
    {
        await LoadPlatformFiles();
        await LoadCores();
        LoadSettings();
        await LoadArchive();
        await LoadBlacklist();
    }

    private async Task LoadPlatformFiles()
    {
        try {
            List<Github.File> files = await GithubApi.GetFiles("dyreschlock", "pocket-platform-images", "arcade/Platforms");
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
        _archiveFiles = await ArchiveService.GetFiles(this._settingsManager.GetConfig().archive_name);
    }

    public async Task LoadDependencies()
    {
        _assets = await AssetsService.GetAssets();
    }
    private async Task LoadBlacklist()
    {
        _blacklist = await AssetsService.GetBlacklist();
    }

    public async Task LoadCores()
    {
        _cores = await CoresService.GetCores();
        foreach(Core core in _cores) {
            core.StatusUpdated += updater_StatusUpdated; //attach handler to bubble event up
        }
    }

    public async Task LoadNonAPICores()
    {
        _cores.AddRange(await CoresService.GetNonAPICores());
    }

    public void LoadSettings()
    {
         _settingsManager = new SettingsManager(SettingsPath, _cores);
    }

    public List<Core> GetMissingCores() => _settingsManager?.GetMissingCores() ?? new List<Core>();

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
        _downloadFirmare = set;
    }

    //get api and local cores
    private async Task<List<Core>> getAllCores()
    {
        List<Core> cores = _cores;
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
        foreach(Core core in _cores) {
            core.UpdateDirectory = UpdateDirectory;
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
    public async Task RunUpdates()
    {
        List<Dictionary<string, string>> installed = new List<Dictionary<string, string>>();
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        Dictionary<string, List<string>> results = new Dictionary<string, List<string>>();
        bool imagesBacked = false;
        string firmwareDownloaded = "";
        if(_cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }

        if(_downloadFirmare) {
            firmwareDownloaded = await UpdateFirmware();
        }

        if(_preservePlatformsFolder) {
            _writeMessage("Backing up platforms folder");
            Util.BackupPlatformsDirectory(UpdateDirectory);
            _writeMessage("Finished backing up platforms folder");
            Divide();
            imagesBacked = true;
        }
        List<Core> cores = await getAllCores();
        string json;
        foreach(Core core in _cores) {
            core.UpdateDirectory = UpdateDirectory;
            core.archive = _settingsManager.GetConfig().archive_name;
            core.downloadAssets = _downloadAssets;
            core.archiveFiles = _archiveFiles;
            core.blacklist = _blacklist;
            core.buildInstances = _settingsManager.GetConfig().build_instance_jsons;
            core.useCRC = _settingsManager.GetConfig().crc_check;
            try {
                if(_settingsManager.GetCoreSettings(core.identifier).skip) {
                    _DeleteCore(core);
                    continue;
                }
                
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }

                _writeMessage("Checking Core: " + name);
                bool allowPrerelease = _settingsManager.GetCoreSettings(core.identifier).allowPrerelease;
                var mostRecentRelease = core.version;

                if(mostRecentRelease == null) {
                    _writeMessage("No releases found. Skipping");
                    results = await core.DownloadAssets();
                    installedAssets.AddRange(results["installed"]);
                    skippedAssets.AddRange(results["skipped"]);
                    await JotegoRename(core);
                    Divide();
                    continue;
                }

                _writeMessage(mostRecentRelease + " is the most recent release, checking local core...");
                if (core.isInstalled()) {
                    Analogue.Core localCore = core.getConfig().core;
                    string localVersion = localCore.metadata.version;
                    
                    if(localVersion != null) {
                        _writeMessage("local core found: " + localVersion);
                    }

                    if (mostRecentRelease != localVersion){
                        _writeMessage("Updating core");
                    } else {
                        results = await core.DownloadAssets();
                        await JotegoRename(core);
                        installedAssets.AddRange(results["installed"]);
                        skippedAssets.AddRange(results["skipped"]);
                        _writeMessage("Up to date. Skipping core");
                        Divide();
                        continue;
                    }
                } else {
                    _writeMessage("Downloading core");
                }
                
                if(await core.Install(UpdateDirectory, _githubApiKey)) {
                    Dictionary<string, string> summary = new Dictionary<string, string>();
                    summary.Add("version", mostRecentRelease);
                    summary.Add("core", core.identifier);
                    summary.Add("platform", core.platform.name);
                    installed.Add(summary);
                }
                await JotegoRename(core);
                results = await core.DownloadAssets();
                installedAssets.AddRange(results["installed"]);
                skippedAssets.AddRange(results["skipped"]);
                _writeMessage("Installation complete.");
                Divide();
                
            } catch(Exception e) {
                _writeMessage("Uh oh something went wrong.");
                _writeMessage(e.Message);
            }
        } 

        if(imagesBacked) {
            _writeMessage("Restoring platforms folder");
            Util.RestorePlatformsDirectory(UpdateDirectory);
            Divide();
        }
        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs();
        args.Message = "Update Process Complete";
        args.InstalledCores = installed;
        args.InstalledAssets = installedAssets;
        args.SkippedAssets = skippedAssets;
        args.FirmwareUpdated = firmwareDownloaded;
        OnUpdateProcessComplete(args);
    }

    private async Task JotegoRename(Core core)
    {
        if(_renameJotegoCores && core.identifier.Contains("jotego")) {
            core.platform_id = core.identifier.Split('.')[1]; //whatever
            string path = Path.Combine(UpdateDirectory, "Platforms", core.platform_id + ".json");
            string json = File.ReadAllText(path);
            Dictionary<string, Platform> data = JsonSerializer.Deserialize<Dictionary<string,Platform>>(json);
            Platform platform = data["platform"];
            if(_platformFiles.ContainsKey(core.platform_id) && platform.name == core.platform_id) {
                _writeMessage("Updating JT Platform Name...");
                await HttpHelper.Instance.DownloadFileAsync(_platformFiles[core.platform_id], path);
                _writeMessage("Complete");
            }
        }
    }

    public async Task RunAssetDownloader(string? id = null)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        Dictionary<string, List<string>> results = new Dictionary<string, List<string>>();
        if(_cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }
        List<Core> cores = await getAllCores();
        foreach(Core core in _cores) {
            if(id != null && core.identifier != id) {
                continue;
            }

            if(_settingsManager.GetCoreSettings(core.identifier).skip) {
                continue;
            }

            core.downloadAssets = true;
            core.UpdateDirectory = UpdateDirectory;
            core.archive = _settingsManager.GetConfig().archive_name;
            core.archiveFiles = _archiveFiles;
            core.blacklist = _blacklist;
            try {
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }
                _writeMessage(core.identifier);
                results = await core.DownloadAssets();
                installedAssets.AddRange(results["installed"]);
                skippedAssets.AddRange(results["skipped"]);
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
        OnUpdateProcessComplete(args);
    }

    private void Divide()
    {
        _writeMessage("-------------");
    }

    private string BuildAssetUrl(DependencyFile asset)
    {
        string archive = _settingsManager.GetConfig().archive_name;
        if(asset.file_name != null && asset.archive_zip == null && asset.archive_file == null && !asset.zip) {
            return ARCHIVE_BASE_URL + "/" + archive + "/" + asset.file_name;
        } else if(archive != null && asset.archive_zip != null) {
            return ARCHIVE_BASE_URL + "/" + archive + "/" + asset.archive_zip + ".zip/" + asset.file_name;
        } else if(archive != null && asset.archive_file != null) {
            return ARCHIVE_BASE_URL + "/" + archive + "/" + asset.archive_file;
        } else if(asset.url != null) {
            return asset.url;
        }

        return "";
    }

    private string BuildAssetUrlNew(string filename)
    {
        string archive = _settingsManager.GetConfig().archive_name;
        return ARCHIVE_BASE_URL + "/" + archive + "/" + filename;
    }

    public async Task<List<Core>> GetLocalCores()
    {
        string coresDirectory = Path.Combine(UpdateDirectory, "Cores");
        string[] directories = Directory.GetDirectories(coresDirectory,"*", SearchOption.TopDirectoryOnly);
        List<Core> all = new List<Core>();
        foreach(string name in directories) {
            string n = Path.GetFileName(name);
            var matches = _cores.Where(i=>i.identifier == n);
            if(matches.Count<Core>() == 0) {
                Core c = new Core {
                    identifier = n
                };
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
    }

    public async Task<string> UpdateFirmware()
    {
        string version = "";
        _writeMessage("Checking for firmware updates...");
        string html = await HttpHelper.Instance.GetHTML(FIRMWARE_URL);
        
        MatchCollection matches = BIN_REGEX.Matches(html);
        if(matches.Count != 1) {
            _writeMessage("cant find it");
            return version;
        } 

        string firmwareUrl = matches[0].Groups["url"].ToString();
        string[] parts = firmwareUrl.Split("/");
        string filename = parts[parts.Length-1];

        Firmware current = _settingsManager.GetCurrentFirmware();
        if(current.version != filename || !File.Exists(Path.Combine(UpdateDirectory, filename))) {
            version = filename;
            var oldfiles = Directory.GetFiles(UpdateDirectory, FIRMWARE_FILENAME_PATTERN);
            _writeMessage("Firmware update found. Downloading...");
            await HttpHelper.Instance.DownloadFileAsync(firmwareUrl, Path.Combine(UpdateDirectory, filename));
            _writeMessage("Download Complete");
            _writeMessage(Path.Combine(UpdateDirectory, filename));
            foreach (string oldfile in oldfiles) {
                if (File.Exists(oldfile) && Path.GetFileName(oldfile) != filename) {
                    _writeMessage("Deleting old firmware file...");
                    File.Delete(oldfile);
                }
            }
            _settingsManager.SetFirmwareVersion(filename);
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

    private void _DeleteCore(Core core)
    {
        if(!_deleteSkippedCores) {
            return;
        }

        core.Uninstall(UpdateDirectory);
    }

    private void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        this.OnStatusUpdated(e);
    }
    public event EventHandler<UpdateProcessCompleteEventArgs>? UpdateProcessComplete;
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
}
