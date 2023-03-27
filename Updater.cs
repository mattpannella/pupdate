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
        Factory.GetGlobals().ArchiveFiles = await ArchiveService.GetFiles(Factory.GetGlobals().SettingsManager.GetConfig().archive_name);
    }

    private async Task LoadBlacklist()
    {
        Factory.GetGlobals().Blacklist = await AssetsService.GetBlacklist();
    }

    public async Task LoadCores()
    {
        Factory.GetGlobals().Cores = await CoresService.GetCores();
        foreach(Core core in Factory.GetGlobals().Cores) {
            core.StatusUpdated += updater_StatusUpdated; //attach handler to bubble event up
        }
    }

    public async Task LoadNonAPICores()
    {
        Factory.GetGlobals().Cores.AddRange(await CoresService.GetNonAPICores());
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
        _downloadFirmare = set;
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
    public async Task RunUpdates()
    {
        List<Dictionary<string, string>> installed = new List<Dictionary<string, string>>();
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        Dictionary<string, List<string>> results = new Dictionary<string, List<string>>();
        bool imagesBacked = false;
        string firmwareDownloaded = "";
        if(Factory.GetGlobals().Cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }

        if(_downloadFirmare) {
            firmwareDownloaded = await UpdateFirmware();
        }

        if(_preservePlatformsFolder) {
            _writeMessage("Backing up platforms folder");
            Util.BackupPlatformsDirectory(Factory.GetGlobals().UpdateDirectory);
            _writeMessage("Finished backing up platforms folder");
            Divide();
            imagesBacked = true;
        }
        List<Core> cores = await getAllCores();
        string json;
        foreach(Core core in Factory.GetGlobals().Cores) {
            core.archive = Factory.GetGlobals().SettingsManager.GetConfig().archive_name;
            core.downloadAssets = _downloadAssets;
            core.buildInstances = Factory.GetGlobals().SettingsManager.GetConfig().build_instance_jsons;
            core.useCRC = Factory.GetGlobals().SettingsManager.GetConfig().crc_check;
            try {
                if(Factory.GetGlobals().SettingsManager.GetCoreSettings(core.identifier).skip) {
                    _DeleteCore(core);
                    continue;
                }
                
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }

                _writeMessage("Checking Core: " + name);
                bool allowPrerelease = Factory.GetGlobals().SettingsManager.GetCoreSettings(core.identifier).allowPrerelease;
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
                
                if(await core.Install(_githubApiKey)) {
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
            Util.RestorePlatformsDirectory(Factory.GetGlobals().UpdateDirectory);
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

    public async Task RunAssetDownloader(string? id = null)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        Dictionary<string, List<string>> results = new Dictionary<string, List<string>>();
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
            core.archive = Factory.GetGlobals().SettingsManager.GetConfig().archive_name;
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
        string archive = Factory.GetGlobals().SettingsManager.GetConfig().archive_name;
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
        string archive = Factory.GetGlobals().SettingsManager.GetConfig().archive_name;
        return ARCHIVE_BASE_URL + "/" + archive + "/" + filename;
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
        string html = await Factory.GetHttpHelper().GetHTML(FIRMWARE_URL);
        
        MatchCollection matches = BIN_REGEX.Matches(html);
        if(matches.Count != 1) {
            _writeMessage("cant find it");
            return version;
        } 

        string firmwareUrl = matches[0].Groups["url"].ToString();
        string[] parts = firmwareUrl.Split("/");
        string filename = parts[parts.Length-1];

        Firmware current = Factory.GetGlobals().SettingsManager.GetCurrentFirmware();
        if(current.version != filename || !File.Exists(Path.Combine(Factory.GetGlobals().UpdateDirectory, filename))) {
            version = filename;
            var oldfiles = Directory.GetFiles(Factory.GetGlobals().UpdateDirectory, FIRMWARE_FILENAME_PATTERN);
            _writeMessage("Firmware update found. Downloading...");
            await Factory.GetHttpHelper().DownloadFileAsync(firmwareUrl, Path.Combine(Factory.GetGlobals().UpdateDirectory, filename));
            _writeMessage("Download Complete");
            _writeMessage(Path.Combine(Factory.GetGlobals().UpdateDirectory, filename));
            foreach (string oldfile in oldfiles) {
                if (File.Exists(oldfile) && Path.GetFileName(oldfile) != filename) {
                    _writeMessage("Deleting old firmware file...");
                    File.Delete(oldfile);
                }
            }
            Factory.GetGlobals().SettingsManager.SetFirmwareVersion(filename);
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

        core.Uninstall();
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
