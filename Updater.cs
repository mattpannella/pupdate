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
        //TODO check if null
        foreach(Core core in coresList) {
            if(core.skip) {
                continue;
            }
            Repo repo = core.repo;
            _writeMessage("Starting Repo: " + repo.project);
            string name = core.name;
            bool allowPrerelease = core.allowPrerelease;
            string url = String.Format(GITHUB_API_URL, repo.user, repo.project);
            string response = await _fetchReleases(url);
            if(response == null) {
                Environment.Exit(1);
            }
            List<Github.Release>? releases = JsonSerializer.Deserialize<List<Github.Release>>(response);
            var mostRecentRelease = _getMostRecentRelease(releases, allowPrerelease);
            
            string tag_name = mostRecentRelease.tag_name;
            List<Github.Asset> assets = mostRecentRelease.assets;

            string releaseSemver = SemverUtil.FindSemver(tag_name);

            Github.Asset coreAsset = null;

            // might need to search for the right zip here if there's more than one
            //iterate through assets to find the zip release
            for(int i = 0; i < assets.Count; i++) {
                if(ZIP_TYPES.Contains(assets[i].content_type)) {
                    coreAsset = assets[i];
                    break;
                }
            }

            if(coreAsset == null) {
                _writeMessage("No zip file found for release. Skipping");
                continue;
            }

            string nameGuess = name ?? coreAsset.name.Split("_")[0];
            _writeMessage(tag_name + " is the most recent release, checking local core...");
            string localCoreFile = Path.Combine(UpdateDirectory, "Cores/"+nameGuess+"/core.json");
            bool fileExists = File.Exists(localCoreFile);

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
                    await _updateCore(coreAsset.browser_download_url);
                    continue;
                }

                if (SemverUtil.SemverCompare(releaseSemver, localSemver)){
                    _writeMessage("Updating core");
                    await _updateCore(coreAsset.browser_download_url);
                } else {
                    _writeMessage("Up to date. Skipping core");
                }
            } else {
                _writeMessage("Downloading core");
                await _updateCore(coreAsset.browser_download_url);
            }
            await SetupBios(core.bios);
            _writeMessage("------------");
        }
    }

    private async Task SetupBios(Bios bios)
    {
        if(_installBios && bios != null) {
            _writeMessage("Looking for BIOS");
            string path = Path.Combine(UpdateDirectory, bios.location);
            foreach(BiosFile file in bios.files) {
                string filePath = Path.Combine(path, file.file_name);
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
            var response = await client.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return responseBody;
        } catch (HttpRequestException e) {
            _writeMessage("Error pulling communicating with Github API.");
            _writeMessage(e.Message);
            return null;
        }
    }

    private async Task _updateCore(string downloadLink)
    {
        _writeMessage("Downloading file " + downloadLink + "...");
        string zipPath = Path.Combine(UpdateDirectory, ZIP_FILE_NAME);
        string extractPath = UpdateDirectory;
        await HttpHelper.DownloadFileAsync(downloadLink, zipPath);

        _writeMessage("Extracting...");
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);
        File.Delete(zipPath);
        _writeMessage("Installation complete.");
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
