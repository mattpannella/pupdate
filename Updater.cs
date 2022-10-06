using System;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http.Headers; 
using System.Text.RegularExpressions;

namespace pannella.analoguepocket;

public class PocketCoreUpdater
{
    private const string ARCHIVE_BASE_URL = "https://archive.org/download";
    private static readonly string[] ZIP_TYPES = {"application/x-zip-compressed", "application/zip"};
    private const string FIRMWARE_URL = "https://www.analogue.co/support/pocket/firmware/latest";
    private const string ZIP_FILE_NAME = "core.zip";
    private static readonly Regex BIN_REGEX = new Regex(@"(?inx)
        <a \s [^>]*
            href \s* = \s*
                (?<q> ['""] )
                    (?<url> [^'""]*\.bin )
                \k<q>
        [^>]* >");
    private bool _downloadAssets = false;

    private string _githubApiKey = "";

    private bool _extractAll = false;
    private bool _downloadFirmare = true;

    private bool _useConsole = false;
    /// <summary>
    /// The directory where fpga cores will be installed and updated into
    /// </summary>
    public string UpdateDirectory { get; set; }

    public string SettingsPath { get; set; }

    private SettingsManager? _settingsManager;

    private List<Core>? _cores;

    private Dictionary<string, Dependency>? _assets;
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="updateDirectory">The directory to install/update openFPGA cores in.</param>
    /// <param name="settingsPath">Path to settings json file</param>
    public PocketCoreUpdater(string updateDirectory, string? settingsPath = null)
    {
        UpdateDirectory = updateDirectory;

        if(settingsPath != null) {
            SettingsPath = settingsPath;
        } else {
            SettingsPath = updateDirectory;
        }
    }

    public async Task Initialize()
    {
        await LoadCores();
        await LoadDependencies();
        LoadSettings();
    }

    public async Task LoadDependencies()
    {
        _assets = await AssetsService.GetAssets();
    }

    public async Task LoadCores()
    {
        _cores = await CoresService.GetCores();
    }

    public void LoadSettings()
    {
         _settingsManager = new SettingsManager(SettingsPath, _cores);
    }

    /// <summary>
    /// Turn on/off printing progress messages to the console
    /// </summary>
    /// <param name="set">Set to true to turn on console messages</param>
    public void PrintToConsole(bool set)
    {
        _useConsole = set;
    }

    /// <summary>
    /// Turn on/off the automatic BIOS downloader
    /// </summary>
    /// <param name="set">Set to true to enable automatic BIOS downloading</param>
    public void DownloadAssets(bool set)
    {
        _downloadAssets = set;
    }

    public void DownloadFirmware(bool set)
    {
        _downloadFirmare = set;
    }

    /// <summary>
    /// Run the full openFPGA core download and update process
    /// </summary>
    public async Task RunUpdates()
    {
        List<string> installed = new List<string>();
        List<string> installedAssets = new List<string>();
        bool firmwareDownloaded = false;
        if(_cores == null) {
            throw new Exception("Must initialize updater before running update process");
        }

        if(_downloadFirmare) {
            firmwareDownloaded = await UpdateFirmware();
        }
        string json;
        foreach(Core core in _cores) {
            try {
                if(_settingsManager.GetCoreSettings(core.identifier).skip) {
                    continue;
                }
                Repo? repo = core.repository;
                _writeMessage("Starting Repo: " + repo.name);
                string name = core.identifier;
                if(name == null) {
                    _writeMessage("Core Name is required. Skipping.");
                    continue;
                }
                bool allowPrerelease = _settingsManager.GetCoreSettings(core.identifier).allowPrerelease;
                List<Github.Release>? releases = await _fetchReleases(repo.owner, repo.name, _githubApiKey);
                if(releases == null) {
                    continue;
                }
                var mostRecentRelease = _getMostRecentRelease(releases, allowPrerelease);
                if(mostRecentRelease == null) {
                    _writeMessage("No releases found. Skipping");
                    continue;
                }
                string tag_name = mostRecentRelease.tag_name;
                List<Github.Asset> assets = mostRecentRelease.assets;

                string releaseSemver = SemverUtil.FindSemver(tag_name);

                _writeMessage(tag_name + " is the most recent release, checking local core...");
                string localCoreFile = Path.Combine(UpdateDirectory, "Cores/"+name+"/core.json");
                bool fileExists = File.Exists(localCoreFile);

                bool foundZip = false;

                if (fileExists) {
                    json = File.ReadAllText(localCoreFile);
                    
                    Analogue.Config? config = JsonSerializer.Deserialize<Analogue.Config>(json);
                    Analogue.Core localCore = config.core;
                    string ver_string = localCore.metadata.version;
                    string localSemver = SemverUtil.FindSemver(ver_string);
                    
                    if(localSemver != null) {
                        _writeMessage("local core found: v" + localSemver);
                    }

                    if (!SemverUtil.IsActuallySemver(localSemver) || !SemverUtil.IsActuallySemver(releaseSemver)) {
                        _writeMessage("downloading core anyway");
                    } else if (SemverUtil.SemverCompare(releaseSemver, localSemver)){
                        _writeMessage("Updating core");
                    } else {
                        if(_assets.ContainsKey(core.identifier)) {
                            var list = await _DownloadAssets(_assets[core.identifier]); //check for roms even if core isn't updating
                            installedAssets.AddRange(list);
                        }
                        _writeMessage("Up to date. Skipping core");
                        _writeMessage("------------");
                        continue;
                    }
                } else {
                    _writeMessage("Downloading core");
                }
                
                // might need to search for the right zip here if there's more than one
                //iterate through assets to find the zip release
                foreach(Github.Asset asset in assets) {
                    if(!ZIP_TYPES.Contains(asset.content_type)) {
                        //not a zip file. move on
                        continue;
                    }
                    foundZip = true;
                    if(await _getAsset(asset.browser_download_url, core.identifier)) {
                        installed.Add(core.identifier + " " + tag_name);
                    }
                }

                if(!foundZip) {
                    _writeMessage("No zip file found for release. Skipping");
                    _writeMessage("------------");
                    continue;
                }
                if(_assets.ContainsKey(core.identifier)) {
                    var list = await _DownloadAssets(_assets[core.identifier]);
                    installedAssets.AddRange(list);
                }
                _writeMessage("Installation complete.");
                _writeMessage("------------");
            } catch(Exception e) {
                _writeMessage("Uh oh something went wrong.");
                _writeMessage(e.Message);
            }
        } 
        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs();
        args.Message = "Update Process Complete";
        args.InstalledCores = installed;
        args.InstalledAssets = installedAssets;
        args.FirmwareUpdated = firmwareDownloaded;
        OnUpdateProcessComplete(args);
    }

    private async Task<List<string>> _DownloadAssets(Dependency assets)
    {
        List<string> installed = new List<string>();
        if(_downloadAssets && assets != null) {
            _writeMessage("Looking for Assets");
            string path = Path.Combine(UpdateDirectory, assets.location);
            if(!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            string filePath;
            foreach(DependencyFile file in assets.files) {
                if(file.overrideLocation != null) { //we have a location override
                    //ensure directory is there. hack to fix gb/gbc issue
                    Directory.CreateDirectory(Path.Combine(UpdateDirectory, file.overrideLocation));
                    filePath = Path.Combine(UpdateDirectory, file.overrideLocation, file.file_name);
                } else {
                    filePath = Path.Combine(path, file.file_name);
                }
                if(File.Exists(filePath)) {
                    _writeMessage("Asset already installed: " + file.file_name);
                } else {
                    string url = BuildAssetUrl(file);
                    if(file.zip) {
                        string zipFile = Path.Combine(path, "roms.zip");
                        _writeMessage("Downloading zip file...");
                        await HttpHelper.DownloadFileAsync(url, zipFile);
                        string extractPath = Path.Combine(UpdateDirectory, "temp");
                        _writeMessage("Extracting files...");
                        ZipFile.ExtractToDirectory(zipFile, extractPath, true);
                        _writeMessage("Moving file: " + file.file_name);
                        File.Move(Path.Combine(extractPath, file.zip_file), filePath);
                        _writeMessage("Deleting temp files");
                        Directory.Delete(extractPath, true);
                        File.Delete(zipFile);
                        installed.Add(filePath);
                    } else {
                        _writeMessage("Downloading " + file.file_name);
                        await HttpHelper.DownloadFileAsync(url, filePath);
                        _writeMessage("Finished downloading " + file.file_name);
                        installed.Add(filePath);
                    }
                }
            }
        }

        return installed;
    }

    private string BuildAssetUrl(DependencyFile asset)
    {
        string archive = _settingsManager.GetConfig().archive_name;
        if(archive != null && asset.archive_zip != null) {
            return ARCHIVE_BASE_URL + "/" + archive + "/" + asset.archive_zip + ".zip/" + asset.file_name;
        } else if(archive != null && asset.archive_file != null) {
            return ARCHIVE_BASE_URL + "/" + archive + "/" + asset.archive_file;
        } else if(asset.url != null) {
            return asset.url;
        }

        return "";
    }

    private Github.Release _getMostRecentRelease(List<Github.Release> releases, bool allowPrerelease)
    {
        foreach(Github.Release release in releases) {
            if(!release.draft && (allowPrerelease || !release.prerelease)) {
                return release;
            }
        }

        return null;
    }

    private async Task<List<Github.Release>?> _fetchReleases(string user, string repository, string token = "")
    {
        try {
            var releases = await GithubApi.GetReleases(user, repository, token);
            return releases;
        } catch (HttpRequestException e) {
            _writeMessage("Error communicating with Github API.");
            _writeMessage(e.Message);
            return null;
        }
    }

    private async Task<bool> _getAsset(string downloadLink, string coreName)
    {
        bool updated = false;
        _writeMessage("Downloading file " + downloadLink + "...");
        string zipPath = Path.Combine(UpdateDirectory, ZIP_FILE_NAME);
        string extractPath = UpdateDirectory;
        await HttpHelper.DownloadFileAsync(downloadLink, zipPath);

        bool isCore = _extractAll;

        if(!isCore) {
            var zip = ZipFile.OpenRead(zipPath);
            foreach(ZipArchiveEntry entry in zip.Entries) {
                string[] parts = entry.FullName.Split('/');
                if(parts.Contains("Cores")) {
                    isCore = true;
                    break;
                }
            }
            zip.Dispose();
        }

        if(!isCore) {
            _writeMessage("Zip does not contain openFPGA core. Skipping. Please use -a if you'd like to extract all zips.");
        } else {
            _writeMessage("Extracting...");
            ZipFile.ExtractToDirectory(zipPath, extractPath, true);
            updated = true;
        }
        File.Delete(zipPath);

        return updated;
    }

    private void _writeMessage(string message)
    {
        if(_useConsole) {
            Console.WriteLine(message);
        }
        StatusUpdatedEventArgs args = new StatusUpdatedEventArgs();
        args.Message = message;
        OnStatusUpdated(args);
    }

    public void SetGithubApiKey(string key)
    {
        _githubApiKey = key;
    }

    protected virtual void OnStatusUpdated(StatusUpdatedEventArgs e)
    {
        EventHandler<StatusUpdatedEventArgs> handler = StatusUpdated;
        if(handler != null)
        {
            handler(this, e);
        }
    }

    protected virtual void OnUpdateProcessComplete(UpdateProcessCompleteEventArgs e)
    {
        EventHandler<UpdateProcessCompleteEventArgs> handler = UpdateProcessComplete;
        if(handler != null)
        {
            handler(this, e);
        }
    }

    public async Task<bool> UpdateFirmware()
    {
        bool flag = false;
        _writeMessage("Checking for firmware updates...");
        string html = await HttpHelper.GetHTML(FIRMWARE_URL);
        
        MatchCollection matches = BIN_REGEX.Matches(html);
        if(matches.Count != 1) {
            _writeMessage("cant find it");
            return false;
        } 

        string firmwareUrl = matches[0].Groups["url"].ToString();
        string[] parts = firmwareUrl.Split("/");
        string filename = parts[parts.Length-1];

        Firmware current = _settingsManager.GetCurrentFirmware();
        if(current.version != filename) {
            flag = true;
            _writeMessage("Firmware update found. Downloading...");
            await HttpHelper.DownloadFileAsync(firmwareUrl, Path.Combine(UpdateDirectory, filename));
            _writeMessage("Download Complete");
            _writeMessage(Path.Combine(UpdateDirectory, filename));
            var oldfile = Path.Combine(UpdateDirectory, current.version);
            if(File.Exists(oldfile)) {
                _writeMessage("Deleting old firmware file...");
                File.Delete(oldfile);
            }
            _settingsManager.SetFirmwareVersion(filename);
            _writeMessage("To install firmware, restart your Pocket.");
        } else {
            _writeMessage("Firmware up to date.");
        }

        return flag;
    }

    public void ExtractAll(bool value)
    {
        _extractAll = value;
    }

    /// <summary>
    /// Event is raised every time the updater prints a progress update
    /// </summary>
    public event EventHandler<StatusUpdatedEventArgs>? StatusUpdated;
    public event EventHandler<UpdateProcessCompleteEventArgs>? UpdateProcessComplete;
}

public class StatusUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Contains the message from the updater
    /// </summary>
    public string Message { get; set; }
}

public class UpdateProcessCompleteEventArgs : EventArgs
{
    /// <summary>
    /// Some kind of results
    /// </summary>
    public string Message { get; set; }
    public List<string> InstalledCores { get; set; }
    public List<string> InstalledAssets { get; set; }
    public bool FirmwareUpdated { get; set; } = false;
}
