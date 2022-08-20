using System;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http.Headers; 

namespace pannella.analoguepocket;

public class PocketCoreUpdater
{
    private const string SEMVER_FINDER = @"\D*(\d(\.\d)*\.\d)\D*";

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

    public PocketCoreUpdater(string coresFile, string updateDirectory)
    {
        UpdateDirectory = updateDirectory;

        //make sure the json file exists
        if(File.Exists(coresFile)) {
            CoresFile = coresFile;
        } else {
            throw new FileNotFoundException("Cores json file not found: " + coresFile);
        }
    }

    public void PrintToConsole(bool set)
    {
        _useConsole = set;
    }

    public void InstallBiosFiles(bool set)
    {
        _installBios = set;
    }

    public async Task RunUpdates()
    {
        string json = File.ReadAllText(CoresFile);
        List<Core>? coresList = JsonSerializer.Deserialize<List<Core>>(json);
        //TODO check if null
        foreach(Core core in coresList) {
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

            Regex r = new Regex(SEMVER_FINDER);
            Match matches = r.Match(tag_name);

            var releaseSemver = matches.Groups[1].Value;
            //TODO throw some error if it doesn't find a semver in the tag
            releaseSemver = _semverFix(releaseSemver);

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

                matches = r.Match(ver_string);
                string localSemver = "";
                if(matches != null && matches.Groups.Count > 1) {
                    localSemver = matches.Groups[1].Value;
                    localSemver = _semverFix(localSemver);
                    _writeMessage("local core found: v" + localSemver);
                }

                if (!_isActuallySemver(localSemver) || !_isActuallySemver(releaseSemver)) {
                    _writeMessage("downloading core anyway");
                    await _updateCore(coreAsset.browser_download_url);
                    continue;
                }

                if (_semverCompare(releaseSemver, localSemver)){
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

    //even though its technically not a valid semver, allow use of 2 part versions, and just add a .0 to complete the 3rd part
    private string _semverFix(string version)
    {
        string[] parts = version.Split(".");

        if(parts.Length == 2) {
            version += ".0";
        }
        
        return version;
    }

    private bool _semverCompare(string semverA, string semverB)
    {
        Version verA = Version.Parse(semverA);
        Version verB = Version.Parse(semverB);
        
        switch(verA.CompareTo(verB))
        {
            case 0:
            case -1:
                return false;
            case 1:
                return true;
            default:
                return true;
        }
    }

    private bool _isActuallySemver(string potentiallySemver)
    {
        Version ver = null;
        return Version.TryParse(potentiallySemver, out ver);
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
    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;
}

public class StatusUpdatedEventArgs : EventArgs
{
    public string Message { get; set; }
}
