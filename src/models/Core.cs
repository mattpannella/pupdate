namespace pannella.analoguepocket;

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Collections;

public class Core : Base
{
    public string identifier { get; set; }
    public Repo? repository { get; set; }
    public Platform? platform { get; set; }
    public string platform_id { get; set; }
    public Sponsor? sponsor { get; set; }
    public string? download_url { get; set; }
    public string? release_date { get; set; }
    public string? version { get; set; }
    public string[]? replaces { get; set; }

    public bool requires_license { get; set; } = false;

    private const string ZIP_FILE_NAME = "core.zip";
    public bool downloadAssets { get; set; } = Factory.GetGlobals().SettingsManager.GetConfig().download_assets;
    public bool buildInstances { get; set; } = Factory.GetGlobals().SettingsManager.GetConfig().build_instance_jsons;


    public override string ToString()
    {
        return platform.name;
    }

    public async Task<bool> Install(string githubApiKey = "")
    {
        if(this.repository == null) {
            _writeMessage("Core installed manually. Skipping.");
            return false;
        }
        //iterate through assets to find the zip release
        return await _installGithubAsset();
    }

    private async Task<bool> _installGithubAsset()
    {
        bool updated = false;
        if (this.download_url == null) {
            _writeMessage("No release URL found...");
            return updated;
        }
        _writeMessage("Downloading file " + this.download_url + "...");
        string zipPath = Path.Combine(Factory.GetGlobals().UpdateDirectory, ZIP_FILE_NAME);
        string extractPath = Factory.GetGlobals().UpdateDirectory;
        await Factory.GetHttpHelper().DownloadFileAsync(this.download_url, zipPath);

        _writeMessage("Extracting...");
        string tempDir = Path.Combine(extractPath, "temp", this.identifier);
        ZipFile.ExtractToDirectory(zipPath, tempDir, true);

        // Clean problematic directories and files.
        Util.CleanDir(tempDir, Factory.GetGlobals().SettingsManager.GetConfig().preserve_platforms_folder, this.platform_id);

        // Move the files into place and delete our core's temp directory.
        _writeMessage("Installing...");
        Util.CopyDirectory(tempDir, extractPath, true, true);
        Directory.Delete(tempDir, true);

        // See if the temp directory itself can be removed.
        // Probably not needed if we aren't going to multithread this, but this is an async function so let's future proof.
        if (!Directory.GetFiles(Path.Combine(extractPath, "temp")).Any())
            Directory.Delete(Path.Combine(extractPath, "temp"));

        updated = true;
        
        File.Delete(zipPath);

        return updated;
    }

    private bool checkUpdateDirectory()
    {
        if(!Directory.Exists(Factory.GetGlobals().UpdateDirectory)) {
            throw new Exception("Unable to access update directory");
        }

        return true;
    }

    public void Uninstall()
    {
        List<string> folders = new List<string>{"Cores", "Presets"};
        foreach(string folder in folders) {
            string path = Path.Combine(Factory.GetGlobals().UpdateDirectory, folder, this.identifier);
            if(Directory.Exists(path)) {
                _writeMessage("Uninstalling " + path);
                Directory.Delete(path, true);
                Divide();
            }
        }
    }

    public Platform? ReadPlatformFile()
    {
        var config = this.getConfig();
        if (config == null)
        {
            return this.platform;
        }
        Analogue.Core info = config.core;
        
        string UpdateDirectory = Factory.GetGlobals().UpdateDirectory;
        //cores with multiple platforms won't work...not sure any exist right now?
        string platformsFolder = Path.Combine(UpdateDirectory, "Platforms");

        string dataFile = Path.Combine(platformsFolder, info.metadata.platform_ids[0] + ".json");
        var p = JsonSerializer.Deserialize<Dictionary<string,Platform>>(File.ReadAllText(dataFile));
        
        return p["platform"];
    }

    public bool UpdatePlatform(string title, string category = null)
    {
        var config = this.getConfig();
        if (config == null)
        {
            return false;
        }
        Analogue.Core info = config.core;
        
        string UpdateDirectory = Factory.GetGlobals().UpdateDirectory;
        //cores with multiple platforms won't work...not sure any exist right now?
        string platformsFolder = Path.Combine(UpdateDirectory, "Platforms");

        string dataFile = Path.Combine(platformsFolder, info.metadata.platform_ids[0] + ".json");
        if (!File.Exists(dataFile))
        {
            return false;
        }

        if (platform.name != title || platform.category != category)
        {
            
            Dictionary<string, Platform> platform = new Dictionary<string, Platform>();
            this.platform.name = title;
            if (category != null)
            {
                this.platform.category = category;
            }
            platform.Add("platform", this.platform);
            string json = JsonSerializer.Serialize(platform);
        
            File.WriteAllText(dataFile, json);
            Factory.GetGlobals().SettingsManager.GetConfig().preserve_platforms_folder = true;
            Factory.GetGlobals().SettingsManager.SaveSettings();
        }
        
        return true;
    }

    public async Task<Dictionary<string, List<string>>> DownloadAssets()
    {
        List<string> installed = new List<string>();
        List<string> skipped = new List<string>();
        if(!downloadAssets || !Factory.GetGlobals().SettingsManager.GetCoreSettings(this.identifier).download_assets) {
            return new Dictionary<string, List<string>>{
                {"installed", installed },
                {"skipped", skipped }
            };
        }
        checkUpdateDirectory();
        _writeMessage("Looking for Assets");
        Analogue.Core info = this.getConfig().core;
        string UpdateDirectory = Factory.GetGlobals().UpdateDirectory;
        //cores with multiple platforms won't work...not sure any exist right now?
        string instancesDirectory = Path.Combine(UpdateDirectory, "Assets", info.metadata.platform_ids[0], this.identifier);
        var options = new JsonSerializerOptions
        {
            Converters = { new StringConverter() }
        };

        Analogue.DataJSON data = ReadDataJSON();
        if(data.data.data_slots.Length > 0) {
            foreach(Analogue.DataSlot slot in data.data.data_slots) {
                if(slot.filename != null && !slot.filename.EndsWith(".sav") && !Factory.GetGlobals().Blacklist.Contains(slot.filename)) {
                    string path = Path.Combine(UpdateDirectory, "Assets", info.metadata.platform_ids[0]);
                    if(slot.isCoreSpecific()) {
                        path = Path.Combine(path, this.identifier);
                    } else {
                        path = Path.Combine(path, "common");
                    }
                    List<string> files = new List<string>();
                    files.Add(slot.filename);
                    if (slot.alternate_filenames != null) {
                        files.AddRange(slot.alternate_filenames);
                    }
                    foreach (string f in files) {
                        string filepath = Path.Combine(path, f);
                        if(File.Exists(filepath) && CheckCRC(filepath)) {
                            _writeMessage("Already installed: " + f);
                        } else {
                            if(await DownloadAsset(f, filepath)) {
                                installed.Add(filepath.Replace(UpdateDirectory, ""));
                            } else {
                                skipped.Add(filepath.Replace(UpdateDirectory, ""));
                            }
                        }
                    }
                }
            }
        }

        if(this.identifier == "Mazamars312.NeoGeo" || this.identifier == "Mazamars312.NeoGeo_Overdrive") {
            return new Dictionary<string, List<string>>{
                {"installed", installed },
                {"skipped", skipped }
            }; //nah
        }

        if(CheckInstancePackager()) {
            BuildInstanceJSONs();
            return new Dictionary<string, List<string>>{
                {"installed", installed },
                {"skipped", skipped }
            };
        }
        
        if(Directory.Exists(instancesDirectory)) {
            string[] files = Directory.GetFiles(instancesDirectory,"*.json", SearchOption.AllDirectories);
            foreach(string file in files) {
                try {
                    //skip mac ._ files
                    if(File.GetAttributes(file).HasFlag(FileAttributes.Hidden)) {
                        continue;
                    }
                    if(Factory.GetGlobals().SettingsManager.GetConfig().skip_alternative_assets && file.Contains(Path.Combine(instancesDirectory, "_alternatives"))) {
                        continue;
                    }
                    Analogue.InstanceJSON instance = JsonSerializer.Deserialize<Analogue.InstanceJSON>(File.ReadAllText(file), options);
                    if(instance.instance.data_slots.Length > 0) {
                        string data_path = instance.instance.data_path;
                        foreach(Analogue.DataSlot slot in instance.instance.data_slots) {
                            if(!Factory.GetGlobals().Blacklist.Contains(slot.filename) && !slot.filename.EndsWith(".sav")) {
                                string path = Path.Combine(UpdateDirectory, "Assets", info.metadata.platform_ids[0], "common", data_path, slot.filename);
                                if(File.Exists(path) && CheckCRC(path)) {
                                    _writeMessage("Already installed: " + slot.filename);
                                } else {
                                    if(await DownloadAsset(slot.filename, path)) {
                                        installed.Add(path.Replace(UpdateDirectory, ""));
                                    } else {
                                        skipped.Add(path.Replace(UpdateDirectory, ""));
                                    }
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    _writeMessage("Error while processing " + file);
                    _writeMessage(e.Message);
                }
            }
        }
        Dictionary<string, List<string>> results = new Dictionary<string, List<string>>{
            {"installed", installed },
            {"skipped", skipped }
        };
        return results;
    }

    public Analogue.Config? getConfig()
    {
        checkUpdateDirectory();
        string file = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores", this.identifier, "core.json");
        if (!File.Exists(file))
        {
            return null;
        }
        string json = File.ReadAllText(file);
        var options = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true
        };
        Analogue.Config? config = JsonSerializer.Deserialize<Analogue.Config>(json, options);

        return config;
    }

    public Updater.Substitute[]? getSubstitutes()
    {
        checkUpdateDirectory();
        string file = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores", this.identifier, "updaters.json");
        if (!File.Exists(file))
        {
            return null;
        }
        string json = File.ReadAllText(file);
        var options = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true
        };
        Updater.Updaters? config = JsonSerializer.Deserialize<Updater.Updaters>(json, options);

        if (config == null) {
            return null;
        }

        return config.previous;
    }

    public bool isInstalled()
    {
        checkUpdateDirectory();
        string localCoreFile = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores", this.identifier, "core.json");
        return File.Exists(localCoreFile);
    }

    private async Task<bool> DownloadAsset(string filename, string destination)
    {
        if(Factory.GetGlobals().ArchiveFiles != null) {
            archiveorg.File? file = Factory.GetGlobals().ArchiveFiles.GetFile(filename);
            if(file == null) {
                _writeMessage("Unable to find " + filename + " in archive");
                return false;
            }
        }

        try {
            string url = BuildAssetUrl(filename);
            int count = 0;
            do {
                _writeMessage("Downloading " + filename);
                await Factory.GetHttpHelper().DownloadFileAsync(url, destination, 600);
                _writeMessage("Finished downloading " + filename);
                count++;
            } while(count < 3 && !CheckCRC(destination));
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
        if(Factory.GetGlobals().SettingsManager.GetConfig().use_custom_archive) {
            var custom = Factory.GetGlobals().SettingsManager.GetConfig().custom_archive;
            Uri baseUrl = new Uri(custom["url"]);
            Uri url = new Uri(baseUrl, filename);
            return url.ToString();
        } else {
            return ARCHIVE_BASE_URL + "/" + Factory.GetGlobals().SettingsManager.GetConfig().archive_name + "/" + filename;
        }
    }

    private bool CheckCRC(string filepath)
    {
        if(Factory.GetGlobals().ArchiveFiles == null || !Factory.GetGlobals().SettingsManager.GetConfig().crc_check) {
            return true;
        }
        string filename = Path.GetFileName(filepath);
        archiveorg.File? file = Factory.GetGlobals().ArchiveFiles.GetFile(filename);
        if(file == null) {
            return true; //no checksum to compare to
        }

        if(Util.CompareChecksum(filepath, file.crc32)) {
            return true;
        }

        _writeMessage(filename + ": Bad checksum!");
        return false;
    }

    public void BuildInstanceJSONs(bool overwrite = true)
    {
        if(!buildInstances) {
            return;
        }
        string instancePackagerFile = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores", this.identifier, "instance-packager.json");
        if(!File.Exists(instancePackagerFile)) {
            return;
        }
        _writeMessage("Building instance json files.");
        InstancePackager packager = JsonSerializer.Deserialize<InstancePackager>(File.ReadAllText(instancePackagerFile));
        string commonPath = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Assets", packager.platform_id, "common");
        string outputDir = Path.Combine(Factory.GetGlobals().UpdateDirectory, packager.output);
        bool warning = false;
        foreach(string dir in Directory.GetDirectories(commonPath, "*", SearchOption.AllDirectories)) {
            Analogue.SimpleInstanceJSON instancejson = new Analogue.SimpleInstanceJSON();
            Analogue.SimpleInstance instance = new Analogue.SimpleInstance();
            string dirName = Path.GetFileName(dir);
            try {
                instance.data_path = dir.Replace(commonPath + Path.DirectorySeparatorChar, "") + "/";
                List<Analogue.InstanceDataSlot> slots = new List<Analogue.InstanceDataSlot>();
                string jsonFileName = dirName + ".json";
                foreach(DataSlot slot in packager.data_slots) {
                    string[] files = Directory.GetFiles(dir, slot.filename);
                    int index = slot.id;
                    switch(slot.sort) {
                        case "single":
                        case "ascending":
                            Array.Sort(files);
                            break;
                        case "descending":
                            IComparer myComparer = new myReverserClass();
                            Array.Sort(files, myComparer);
                            break;
                    }
                    if(slot.required && files.Count() == 0) {
                        throw new MissingRequiredInstanceFiles("Missing required files.");
                    }
                    foreach(string file in files) {
                        if(File.GetAttributes(file).HasFlag(FileAttributes.Hidden)) {
                            continue;
                        }
                        Analogue.InstanceDataSlot current = new Analogue.InstanceDataSlot();
                        string filename = Path.GetFileName(file);
                        if(slot.as_filename) {
                            jsonFileName = Path.GetFileNameWithoutExtension(file) + ".json";
                        }
                        current.id = index.ToString();
                        current.filename = filename;
                        index++;
                        slots.Add(current);
                    }
                }
                var limit = (JsonElement)packager.slot_limit["count"];
                if (slots.Count == 0 || (packager.slot_limit != null && slots.Count > limit.GetInt32())) {
                    _writeMessage("Unable to build " + jsonFileName);
                    warning = true;
                    continue;
                }
                instance.data_slots = slots.ToArray();
                instancejson.instance = instance;
                var options = new JsonSerializerOptions()
                {
                    WriteIndented = true
                };
                string[] parts = dir.Split(commonPath);
                parts = parts[1].Split(jsonFileName.Remove(jsonFileName.Length - 5));
                string subdir = "";
                if(parts[0].Length > 1) {
                    subdir = parts[0].Trim(Path.DirectorySeparatorChar);
                }
                string outputfile = Path.Combine(Factory.GetGlobals().UpdateDirectory, packager.output, subdir, jsonFileName);
                if(!overwrite && File.Exists(outputfile)) {
                    _writeMessage(jsonFileName + " already exists.");
                } else {
                    string json = JsonSerializer.Serialize<Analogue.SimpleInstanceJSON>(instancejson, options);
                    _writeMessage("Saving " + jsonFileName);
                    FileInfo file = new System.IO.FileInfo(outputfile);
                    file.Directory.Create(); // If the directory already exists, this method does nothing.
                    File.WriteAllText(outputfile, json);
                }
            } catch(MissingRequiredInstanceFiles) {
                //do nothin
            } catch(Exception e) {
                _writeMessage("Unable to build " + dirName);
            }
        }
        if (warning) {
            var message = (JsonElement)packager.slot_limit["message"];
            _writeMessage(message.GetString());
        }
        _writeMessage("Finished");
    }

    public bool CheckInstancePackager()
    {
        string instancePackagerFile = Path.Combine(Factory.GetGlobals().UpdateDirectory, "Cores", this.identifier, "instance-packager.json");
        return File.Exists(instancePackagerFile);
    }

    public Analogue.DataJSON ReadDataJSON()
    {
        string UpdateDirectory = Factory.GetGlobals().UpdateDirectory;
        string coreDirectory = Path.Combine(UpdateDirectory, "Cores", this.identifier);
        string dataFile = Path.Combine(coreDirectory, "data.json");
        var options = new JsonSerializerOptions
        {
            Converters = { new StringConverter() }
        };

        Analogue.DataJSON data = JsonSerializer.Deserialize<Analogue.DataJSON>(File.ReadAllText(dataFile), options);

        return data;
    }

    public bool JTBetaCheck()
    {
        var data = ReadDataJSON();
        return data.data.data_slots.Any(x=>x.name=="JTBETA");
    }

    public async Task ReplaceCheck()
    {
        if (replaces != null) {
            foreach(string id in replaces) {
                Core c = new Core(){identifier = id};
                if (c.isInstalled()) {
                    c.Uninstall();
                    _writeMessage($"Uninstalled {id}. It was replaced by this core.");
                }
            }
        }
    }
}
public class myReverserClass : IComparer  {

      // Calls CaseInsensitiveComparer.Compare with the parameters reversed.
      int IComparer.Compare( Object x, Object y )  {
          return( (new CaseInsensitiveComparer()).Compare( y, x ) );
      }
   }
