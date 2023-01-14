namespace pannella.analoguepocket;

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

public class Core : Base
{
    public string? identifier { get; set; }
    public Repo? repository { get; set; }
    public string? platform { get; set; }
    public Release release { get; set; }
    public Release? prerelease { get; set; }
    public bool mono { get; set; }
    public Sponsor? sponsor { get; set; }
    public string? release_path { get; set; }
    public string version_type { get; set; } = "date";

    private static readonly string[] ZIP_TYPES = {"application/x-zip-compressed", "application/zip"};
    private const string ZIP_FILE_NAME = "core.zip";

    public string UpdateDirectory { get; set; }
    public string archive { get; set; }
    private bool _extractAll = false;
    public bool downloadAssets { get; set; } = true;

    public override string ToString()
    {
        return platform;
    }

    public async Task<bool> Install(string UpdateDirectory, string tag_name, string githubApiKey = "", bool extractAll = false)
    {
        if(this.repository == null) {
            _writeMessage("Core installed manually. Skipping.");
            return false;
        }
        this.UpdateDirectory = UpdateDirectory;
        this._extractAll = extractAll;
        if(this.mono) {
            await _fetchCustomRelease(githubApiKey);
        } else {
            //iterate through assets to find the zip release
            var release = await _fetchRelease(tag_name, githubApiKey);
            bool foundZip = false;
            List<Github.Asset> assets = release.assets;
            foreach(Github.Asset asset in assets) {
                if(!ZIP_TYPES.Contains(asset.content_type)) {
                    //not a zip file. move on
                    continue;
                }
                foundZip = true;
                await _installGithubAsset(asset.browser_download_url, this.identifier);
            }

            if(!foundZip) {
                _writeMessage("No zip file found for release. Skipping");
                Divide();
                return false;
            }
        }
        return true;
    }

    private async Task<Github.Release>? _fetchRelease(string tag_name, string token = "")
    {
        try {
            var release = await GithubApi.GetRelease(this.repository.owner, this.repository.name, tag_name, token);
            return release;
        } catch (HttpRequestException e) {
            _writeMessage("Error communicating with Github API.");
            _writeMessage(e.Message);
            return null;
        }
    }

    private async Task<bool> _fetchCustomRelease(string githubApiKey)
    {
        string zip_name = this.identifier + "_" + this.release.tag_name + ".zip";
        Github.File file = await GithubApi.GetFile(this.repository.owner, this.repository.name, this.release_path + "/" + zip_name, githubApiKey);

        _writeMessage("Downloading file " + file.download_url + "...");
        string zipPath = Path.Combine(this.UpdateDirectory, ZIP_FILE_NAME);
        string extractPath = this.UpdateDirectory;
        await HttpHelper.DownloadFileAsync(file.download_url, zipPath);

        _writeMessage("Extracting...");

        string tempDir = Path.Combine(extractPath, "temp", identifier ?? release.tag_name);
        ZipFile.ExtractToDirectory(zipPath, tempDir, true);

        // Clean problematic directories and files.
        Util.CleanDir(tempDir);

        // Move the files into place and delete our core's temp directory.
        _writeMessage("Installing...");
        Util.CopyDirectory(tempDir, extractPath, true, true);
        Directory.Delete(tempDir, true);

        // See if the temp directory itself can be removed.
        // Probably not needed if we aren't going to multithread this, but this is an async function so let's future proof.
        if(!Directory.GetFiles(Path.Combine(extractPath, "temp")).Any())
            Directory.Delete(Path.Combine(extractPath, "temp"));

        File.Delete(zipPath);

        return true;
    }

    private async Task<bool> _installGithubAsset(string downloadLink, string coreName)
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
            string tempDir = Path.Combine(extractPath, "temp", coreName);
            ZipFile.ExtractToDirectory(zipPath, tempDir, true);

            // Clean problematic directories and files.
            Util.CleanDir(tempDir);

            // Move the files into place and delete our core's temp directory.
            _writeMessage("Installing...");
            Util.CopyDirectory(tempDir, extractPath, true, true);
            Directory.Delete(tempDir, true);

            // See if the temp directory itself can be removed.
            // Probably not needed if we aren't going to multithread this, but this is an async function so let's future proof.
            if (!Directory.GetFiles(Path.Combine(extractPath, "temp")).Any())
                Directory.Delete(Path.Combine(extractPath, "temp"));

            updated = true;
        }
        File.Delete(zipPath);

        return updated;
    }

    private bool checkUpdateDirectory()
    {
        if(this.UpdateDirectory == null) {
            throw new Exception("Didn't set an update directory");
        }
        if(!Directory.Exists(this.UpdateDirectory)) {
            throw new Exception("Unable to access update directory");
        }

        return true;
    }

    public void Uninstall(string UpdateDirectory)
    {
        List<string> folders = new List<string>{"Cores", "Presets"};
        foreach(string folder in folders) {
            string path = Path.Combine(UpdateDirectory, folder, this.identifier);
            if(Directory.Exists(path)) {
                _writeMessage("Uninstalling " + path);
                Directory.Delete(path, true);
                Divide();
            }
        }
    }

    public async Task<List<string>> DownloadAssets()
    {
        List<string> installed = new List<string>();
        if(!downloadAssets) {
            return installed;
        }
        checkUpdateDirectory();
        _writeMessage("Looking for Assets");
        Analogue.Core info = this.getConfig().core;
        string coreDirectory = Path.Combine(UpdateDirectory, "Cores", this.identifier);
        //cores with multiple platforms won't work...not sure any exist right now?
        string instancesDirectory = Path.Combine(UpdateDirectory, "Assets", info.metadata.platform_ids[0], this.identifier);

        string dataFile = Path.Combine(coreDirectory, "data.json");
        var options = new JsonSerializerOptions
        {
            Converters = { new StringConverter() }
        };

        Analogue.DataJSON data = JsonSerializer.Deserialize<Analogue.DataJSON>(File.ReadAllText(dataFile), options);
        if(data.data.data_slots.Length > 0) {
            foreach(Analogue.DataSlot slot in data.data.data_slots) {
                if(slot.filename != null) {
                    string path = Path.Combine(UpdateDirectory, "Assets", info.metadata.platform_ids[0]);
                    if(slot.isCoreSpecific()) {
                        path = Path.Combine(path, this.identifier);
                    } else {
                        path = Path.Combine(path, "common");
                    }
                    path = Path.Combine(path, slot.filename);
                    if(File.Exists(path)) {
                        _writeMessage("Already installed: " + slot.filename);
                    } else {
                        if(await DownloadAsset(slot.filename, path)) {
                            installed.Add(path);
                        }
                    }
                    
                }
            }
        }

        if(this.identifier == "Mazamars312.NeoGeo" || this.identifier == "Mazamars312.NeoGeo_Overdrive") {
            return installed; //nah
        }
        if(Directory.Exists(instancesDirectory)) {
            string[] files = Directory.GetFiles(instancesDirectory,"*.json", SearchOption.AllDirectories);
            foreach(string file in files) {
                try {
                    //skip mac ._ files
                    if(File.GetAttributes(file).HasFlag(FileAttributes.Hidden)) {
                        continue;
                    }
                    Analogue.InstanceJSON instance = JsonSerializer.Deserialize<Analogue.InstanceJSON>(File.ReadAllText(file), options);
                    if(instance.instance.data_slots.Length > 0) {
                        string data_path = instance.instance.data_path;
                        foreach(Analogue.DataSlot slot in instance.instance.data_slots) {
                            string path = Path.Combine(UpdateDirectory, "Assets", info.metadata.platform_ids[0], "common", data_path, slot.filename);
                            if(File.Exists(path)) {
                                _writeMessage("Already installed: " + slot.filename);
                            } else {
                                if(await DownloadAsset(slot.filename, path)) {
                                    installed.Add(path);
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    _writeMessage("Unable to read " + file);
                    _writeMessage(e.Message);
                }
            }
        }

        return installed;
    }

    public Analogue.Config? getConfig()
    {
        checkUpdateDirectory();
        string file = Path.Combine(UpdateDirectory, "Cores", this.identifier, "core.json");
        string json = File.ReadAllText(file);
        Analogue.Config? config = JsonSerializer.Deserialize<Analogue.Config>(json);

        return config;
    }

    public bool isInstalled()
    {
        checkUpdateDirectory();
        string localCoreFile = Path.Combine(UpdateDirectory, "Cores", this.identifier, "core.json");
        return File.Exists(localCoreFile);
    }

    private async Task<bool> DownloadAsset(string filename, string destination)
    {
        try {
            string url = BuildAssetUrl(filename);
            _writeMessage("Downloading " + filename);
            await HttpHelper.DownloadFileAsync(url, destination);
            _writeMessage("Finished downloading " + filename);
        } catch(HttpRequestException e) {
            if(e.StatusCode == System.Net.HttpStatusCode.NotFound) {
                _writeMessage("Unable to find " + filename + " in archive");
            } else {
                _writeMessage("There was a problem downloading " + filename);
            }

            return false;
        }

        return true;
    }

    private string BuildAssetUrl(string filename)
    {
        return ARCHIVE_BASE_URL + "/" + archive + "/" + filename;
    }
}
