using System;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http.Headers; 

namespace pannella.analoguepocket;

public class PocketCoreUpdater
{
    private const string GITHUB_API_URL = "https://api.github.com/repos/{0}/{1}/releases";
    private static readonly string[] ZIP_TYPES = {"application/x-zip-compressed", "application/zip"};
    private const string ZIP_FILE_NAME = "core.zip";
    private bool _installBios = false;

    private string _githubApiKey;

    private bool _useConsole = false;
    /// <summary>
    /// The directory where fpga cores will be installed and updated into
    /// </summary>
    public string UpdateDirectory { get; set; }
    /// <summary>
    /// The json file containing the list of cores to check
    /// </summary>
    public string CoresFile { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="updateDirectory">The directory to install/update openFPGA cores in.</param>
    /// <param name="coresFile">Path to cores json file</param>
    public PocketCoreUpdater(string updateDirectory, string coresFile = null)
    {
        UpdateDirectory = updateDirectory;

        //make sure the json file exists
        if(coresFile != null) {
            if(File.Exists(coresFile)) {
                CoresFile = coresFile;
            } else {
                throw new FileNotFoundException("Cores json file not found: " + coresFile);
            }
        }
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
    public void InstallBiosFiles(bool set)
    {
        _installBios = set;
    }

    /// <summary>
    /// Run the full openFPGA core download and update process
    /// </summary>
    public async Task RunUpdates()
    {
        if(CoresFile == null) {
            throw new Exception("No Cores file has been set");
        }
        string json = File.ReadAllText(CoresFile);
        List<Core>? coresList = JsonSerializer.Deserialize<List<Core>>(json);

        foreach(Core core in coresList) {
            if(core.skip) {
                continue;
            }
            Repo repo = core.repo;
            _writeMessage("Starting Repo: " + repo.project);
            string name = core.name;
            if(name == null) {
                _writeMessage("Core Name is required. Skipping.");
                continue;
            }
            bool allowPrerelease = core.allowPrerelease;
            string url = String.Format(GITHUB_API_URL, repo.user, repo.project);
            string response = await _fetchReleases(url);
            if(response == null) {
                Environment.Exit(1);
            }
            List<Github.Release>? releases = JsonSerializer.Deserialize<List<Github.Release>>(response);
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
                    await SetupBios(core.bios); //check for bios even if core isn't updating
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
                await _getAsset(asset.browser_download_url);
            }

            if(!foundZip) {
                _writeMessage("No zip file found for release. Skipping");
                _writeMessage("------------");
                continue;
            }
            await SetupBios(core.bios);
            _writeMessage("Installation complete.");
            _writeMessage("------------");
        }
    }

    private async Task SetupBios(Bios bios)
    {
        if(_installBios && bios != null) {
            _writeMessage("Looking for BIOS");
            string path = Path.Combine(UpdateDirectory, bios.location);
            string filePath;
            foreach(BiosFile file in bios.files) {
                if(file.overrideLocation != null) { //we have a location override
                    filePath = Path.Combine(UpdateDirectory, file.overrideLocation, file.file_name);
                } else {
                    filePath = Path.Combine(path, file.file_name);
                }
                if(File.Exists(filePath)) {
                    _writeMessage("BIOS file already installed: " + file.file_name);
                } else {
                    if(file.zip) {
                        string zipFile = Path.Combine(path, "bios.zip");
                        _writeMessage("Downloading zip file...");
                        await HttpHelper.DownloadFileAsync(file.url, zipFile);
                        string extractPath = Path.Combine(UpdateDirectory, "temp");
                        _writeMessage("Extracting files...");
                        ZipFile.ExtractToDirectory(zipFile, extractPath, true);
                        _writeMessage("Moving file: " + file.file_name);
                        File.Move(Path.Combine(extractPath, file.zip_file), filePath);
                        _writeMessage("Deleting temp files");
                        Directory.Delete(extractPath, true);
                        File.Delete(zipFile);
                    } else {
                        _writeMessage("Downloading " + file.file_name);
                        await HttpHelper.DownloadFileAsync(file.url, filePath);
                        _writeMessage("Finished downloading " + file.file_name);
                    }
                }
            }
        }
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

    private async Task<string> _fetchReleases(string url)
    {
        try {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };
            var agent = new ProductInfoHeaderValue("Analogue-Pocket-Auto-Updater", "1.0");
            request.Headers.UserAgent.Add(agent);
            if(_githubApiKey != null) {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("token", _githubApiKey);
            }
            var response = await client.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return responseBody;
        } catch (HttpRequestException e) {
            _writeMessage("Error communicating with Github API.");
            _writeMessage(e.Message);
            return null;
        }
    }

    private async Task _getAsset(string downloadLink)
    {
        _writeMessage("Downloading file " + downloadLink + "...");
        string zipPath = Path.Combine(UpdateDirectory, ZIP_FILE_NAME);
        string extractPath = UpdateDirectory;
        await HttpHelper.DownloadFileAsync(downloadLink, zipPath);

        _writeMessage("Extracting...");
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);
        File.Delete(zipPath);
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

    /// <summary>
    /// Event is raised every time the updater prints a progress update
    /// </summary>
    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;
}

public class StatusUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Contains the message from the updater
    /// </summary>
    public string Message { get; set; }
}
