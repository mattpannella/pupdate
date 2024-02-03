using System.Collections;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Analogue.Data;
using Pannella.Models.Analogue.Instance;
using Pannella.Models.Analogue.Instance.Simple;
using Pannella.Models.Analogue.Video;
using Pannella.Models.InstancePackager;
using Pannella.Models.Updater;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;
using ArchiveFile = Pannella.Models.Archive.File;
using DataSlot = Pannella.Models.Analogue.Shared.DataSlot;
using InstancePackagerDataSlot =  Pannella.Models.InstancePackager.DataSlot;

namespace Pannella.Models;

public class Core : Base
{
    public string identifier { get; set; }
    public Repo repository { get; set; }
    public Platform platform { get; set; }
    public string platform_id { get; set; }
    public Sponsor sponsor { get; set; }
    public string download_url { get; set; }
    public string release_date { get; set; }
    public string version { get; set; }
    public string beta_slot_id;
    public int beta_slot_platform_id_index;
    public bool requires_license { get; set; } = false;

    public bool download_assets { get; set; } = GlobalHelper.SettingsManager.GetConfig().download_assets;

    public bool build_instances { get; set; } = GlobalHelper.SettingsManager.GetConfig().build_instance_jsons;

    private const string ZIP_FILE_NAME = "core.zip";

    private static string[] ALL_MODES = { "0x10", "0x20", "0x30", "0x31", "0x32", "0x40", "0x41", "0x42", "0x51", "0x52", "0xE0" };

    private static string[] GB_MODES = { "0x21", "0x22", "0x23" };

    public override string ToString()
    {
        return platform.name;
    }

    public async Task<bool> Install(bool preservePlatformsFolder, bool clean = false)
    {
        if (this.repository == null)
        {
            WriteMessage("Core installed manually. Skipping.");

            return false;
        }

        if (clean && this.IsInstalled())
        {
            Delete();
        }

        // iterate through assets to find the zip release
        if (await InstallGithubAsset(preservePlatformsFolder))
        {
            this.ReplaceCheck();

            return true;
        }

        return false;
    }

    private async Task<bool> InstallGithubAsset(bool preservePlatformsFolder)
    {
        if (this.download_url == null)
        {
            WriteMessage("No release URL found...");

            return false;
        }

        WriteMessage("Downloading file " + this.download_url + "...");

        string zipPath = Path.Combine(GlobalHelper.UpdateDirectory, ZIP_FILE_NAME);
        string extractPath = GlobalHelper.UpdateDirectory;

        await HttpHelper.Instance.DownloadFileAsync(this.download_url, zipPath);

        WriteMessage("Extracting...");

        string tempDir = Path.Combine(extractPath, "temp", this.identifier);

        ZipFile.ExtractToDirectory(zipPath, tempDir, true);

        // Clean problematic directories and files.
        Util.CleanDir(tempDir, preservePlatformsFolder, this.platform_id);

        // Move the files into place and delete our core's temp directory.
        WriteMessage("Installing...");
        Util.CopyDirectory(tempDir, extractPath, true, true);
        Directory.Delete(tempDir, true);

        // See if the temp directory itself can be removed.
        // Probably not needed if we aren't going to multi-thread this, but this is an async function so let's future proof.
        if (!Directory.GetFiles(Path.Combine(extractPath, "temp")).Any())
        {
            Directory.Delete(Path.Combine(extractPath, "temp"));
        }

        File.Delete(zipPath);

        return true;
    }

    private static void CheckUpdateDirectory()
    {
        if (!Directory.Exists(GlobalHelper.UpdateDirectory))
        {
            throw new Exception("Unable to access update directory");
        }
    }

    private void Delete(bool nuke = false)
    {
        List<string> folders = new List<string> { "Cores", "Presets", "Settings" };

        foreach (string folder in folders)
        {
            string path = Path.Combine(GlobalHelper.UpdateDirectory, folder, this.identifier);

            if (Directory.Exists(path))
            {
                WriteMessage("Deleting " + path);
                Directory.Delete(path, true);
            }
        }

        if (nuke)
        {
            string path = Path.Combine(GlobalHelper.UpdateDirectory, "Assets", this.platform_id, this.identifier);

            if (Directory.Exists(path))
            {
                WriteMessage("Deleting " + path);
                Directory.Delete(path, true);
            }
        }
    }

    public void Uninstall(bool nuke = false)
    {
        WriteMessage("Uninstalling " + this.identifier);

        Delete(nuke);

        GlobalHelper.SettingsManager.DisableCore(this.identifier);
        GlobalHelper.SettingsManager.SaveSettings();

        WriteMessage("Finished");
        Divide();
    }

    public Platform ReadPlatformFile()
    {
        var info = this.GetConfig();

        if (info == null)
        {
            return this.platform;
        }

        string updateDirectory = GlobalHelper.UpdateDirectory;
        // cores with multiple platforms won't work...not sure any exist right now?
        string platformsFolder = Path.Combine(updateDirectory, "Platforms");
        string dataFile = Path.Combine(platformsFolder, info.metadata.platform_ids[0] + ".json");
        var p = JsonSerializer.Deserialize<Dictionary<string, Platform>>(File.ReadAllText(dataFile));

        return p["platform"];
    }

    public async Task<Dictionary<string, object>> DownloadAssets()
    {
        List<string> installed = new List<string>();
        List<string> skipped = new List<string>();
        bool missingBetaKey = false;

        if (!this.download_assets || !GlobalHelper.SettingsManager.GetCoreSettings(this.identifier).download_assets)
        {
            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingBetaKey", false }
            };
        }

        CheckUpdateDirectory();
        WriteMessage("Looking for Assets");
        AnalogueCore info = this.GetConfig();
        string updateDirectory = GlobalHelper.UpdateDirectory;
        // cores with multiple platforms won't work...not sure any exist right now?
        string instancesDirectory = Path.Combine(updateDirectory, "Assets", info.metadata.platform_ids[0], this.identifier);
        var options = new JsonSerializerOptions { Converters = { new StringConverter() } };

        DataJSON dataJson = ReadDataJSON();

        if (this.beta_slot_id != null)
        {
            // what to do?
        }

        if (dataJson.data.data_slots.Length > 0)
        {
            foreach (DataSlot slot in dataJson.data.data_slots)
            {
                if (slot.filename != null && !slot.filename.EndsWith(".sav") &&
                    !GlobalHelper.Blacklist.Contains(slot.filename))
                {
                    string path = Path.Combine(updateDirectory, "Assets", info.metadata.platform_ids[0]);

                    if (slot.IsCoreSpecific())
                    {
                        path = Path.Combine(path, this.identifier);
                    }
                    else
                    {
                        path = Path.Combine(path, "common");
                    }

                    List<string> files = new List<string> { slot.filename };

                    if (slot.alternate_filenames != null)
                    {
                        files.AddRange(slot.alternate_filenames);
                    }

                    foreach (string f in files)
                    {
                        string filepath = Path.Combine(path, f);

                        if (File.Exists(filepath) && CheckCRC(filepath))
                        {
                            WriteMessage("Already installed: " + f);
                        }
                        else
                        {
                            if (await DownloadAsset(f, filepath))
                            {
                                installed.Add(filepath.Replace(updateDirectory, ""));
                            }
                            else
                            {
                                skipped.Add(filepath.Replace(updateDirectory, ""));
                            }
                        }
                    }
                }
            }
        }

        if (this.identifier is "Mazamars312.NeoGeo" or "Mazamars312.NeoGeo_Overdrive")
        {
            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingBetaKey", false }
            };
        }

        if (CheckInstancePackager())
        {
            BuildInstanceJSONs();

            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingBetaKey", false }
            };
        }

        if (Directory.Exists(instancesDirectory))
        {
            string[] files = Directory.GetFiles(instancesDirectory, "*.json", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                try
                {
                    // skip mac ._ files
                    if (File.GetAttributes(file).HasFlag(FileAttributes.Hidden))
                    {
                        continue;
                    }

                    if (GlobalHelper.SettingsManager.GetConfig().skip_alternative_assets &&
                        file.Contains(Path.Combine(instancesDirectory, "_alternatives")))
                    {
                        continue;
                    }

                    InstanceJSON instanceJson = JsonSerializer.Deserialize<InstanceJSON>(File.ReadAllText(file), options);

                    if (instanceJson.instance.data_slots.Length > 0)
                    {
                        string dataPath = instanceJson.instance.data_path;

                        foreach (DataSlot slot in instanceJson.instance.data_slots)
                        {
                            var plat = info.metadata.platform_ids[this.beta_slot_platform_id_index];

                            if (!CheckBetaMD5(slot, plat))
                            {
                                WriteMessage("Invalid or missing beta key.");
                                missingBetaKey = true;
                            }

                            if (!GlobalHelper.Blacklist.Contains(slot.filename) &&
                                !slot.filename.EndsWith(".sav"))
                            {
                                string path = Path.Combine(updateDirectory, "Assets", info.metadata.platform_ids[0],
                                    "common", dataPath, slot.filename);

                                if (File.Exists(path) && CheckCRC(path))
                                {
                                    WriteMessage("Already installed: " + slot.filename);
                                }
                                else
                                {
                                    if (await DownloadAsset(slot.filename, path))
                                    {
                                        installed.Add(path.Replace(updateDirectory, ""));
                                    }
                                    else
                                    {
                                        skipped.Add(path.Replace(updateDirectory, ""));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    WriteMessage("Error while processing " + file);
                    WriteMessage(e.Message);
                }
            }
        }

        Dictionary<string, object> results = new Dictionary<string, object>
        {
            { "installed", installed },
            { "skipped", skipped },
            { "missingBetaKey", missingBetaKey }
        };
        return results;
    }

    public AnalogueCore GetConfig()
    {
        CheckUpdateDirectory();

        string file = Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "core.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        var options = new JsonSerializerOptions { AllowTrailingCommas = true };
        AnalogueCore config = JsonSerializer.Deserialize<Dictionary<string, AnalogueCore>>(json, options)["core"];

        return config;
    }

    public Substitute[] GetSubstitutes()
    {
        CheckUpdateDirectory();

        string file = Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "updaters.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        Updaters config = JsonSerializer.Deserialize<Updaters>(json);

        return config?.previous;
    }

    public bool IsInstalled()
    {
        CheckUpdateDirectory();

        string localCoreFile = Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "core.json");

        return File.Exists(localCoreFile);
    }

    private async Task<bool> DownloadAsset(string filename, string destination)
    {
        if (GlobalHelper.ArchiveFiles != null)
        {
            ArchiveFile file = GlobalHelper.ArchiveFiles.GetFile(filename);

            if (file == null)
            {
                WriteMessage("Unable to find " + filename + " in archive");
                return false;
            }
        }

        try
        {
            string url = BuildAssetUrl(filename);
            int count = 0;

            do
            {
                WriteMessage("Downloading " + filename);
                await HttpHelper.Instance.DownloadFileAsync(url, destination, 600);
                WriteMessage("Finished downloading " + filename);
                count++;
            }
            while (count < 3 && !CheckCRC(destination));
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                WriteMessage("Unable to find " + filename + " in archive");
            }
            else
            {
                WriteMessage("There was a problem downloading " + filename);
            }

            return false;
        }

        return true;
    }

    private static string BuildAssetUrl(string filename)
    {
        if (GlobalHelper.SettingsManager.GetConfig().use_custom_archive)
        {
            var custom = GlobalHelper.SettingsManager.GetConfig().custom_archive;
            Uri baseUrl = new Uri(custom["url"]);
            Uri url = new Uri(baseUrl, filename);
            return url.ToString();
        }

        return ARCHIVE_BASE_URL + "/" + GlobalHelper.SettingsManager.GetConfig().archive_name + "/" + filename;
    }

    private bool CheckCRC(string filepath)
    {
        if (GlobalHelper.ArchiveFiles == null || !GlobalHelper.SettingsManager.GetConfig().crc_check)
        {
            return true;
        }

        string filename = Path.GetFileName(filepath);
        ArchiveFile file = GlobalHelper.ArchiveFiles.GetFile(filename);

        if (file == null)
        {
            return true; //no checksum to compare to
        }

        if (Util.CompareChecksum(filepath, file.crc32))
        {
            return true;
        }

        WriteMessage(filename + ": Bad checksum!");
        return false;
    }

    // return false if a beta ley is required and missing or wrong
    private bool CheckBetaMD5(DataSlot slot, string platform)
    {
        if (slot.md5 != null && (this.beta_slot_id != null && slot.id == this.beta_slot_id))
        {
            string updateDirectory = GlobalHelper.UpdateDirectory;
            string path = Path.Combine(updateDirectory, "Assets", platform);
            string filepath = Path.Combine(path, "common", slot.filename);

            return File.Exists(filepath) && Util.CompareChecksum(filepath, slot.md5, Util.HashTypes.MD5);
        }

        return true;
    }

    public void BuildInstanceJSONs(bool overwrite = true)
    {
        if (!this.build_instances)
        {
            return;
        }

        string instancePackagerFile = Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "instance-packager.json");

        if (!File.Exists(instancePackagerFile))
        {
            return;
        }

        WriteMessage("Building instance json files.");
        InstanceJsonPackager jsonPackager = JsonSerializer.Deserialize<InstanceJsonPackager>(File.ReadAllText(instancePackagerFile));
        string commonPath = Path.Combine(GlobalHelper.UpdateDirectory, "Assets", jsonPackager.platform_id, "common");
        bool warning = false;

        foreach (string dir in Directory.GetDirectories(commonPath, "*", SearchOption.AllDirectories))
        {
            SimpleInstanceJSON simpleInstanceJson = new SimpleInstanceJSON();
            SimpleInstance instance = new SimpleInstance();
            string dirName = Path.GetFileName(dir);

            try
            {
                instance.data_path = dir.Replace(commonPath + Path.DirectorySeparatorChar, "") + "/";

                List<SimpleDataSlot> slots = new();
                string jsonFileName = dirName + ".json";

                foreach (InstancePackagerDataSlot slot in jsonPackager.data_slots)
                {
                    string[] files = Directory.GetFiles(dir, slot.filename);
                    int index = slot.id;

                    switch (slot.sort)
                    {
                        case "single":
                        case "ascending":
                            Array.Sort(files);
                            break;

                        case "descending":
                            IComparer myComparer = new ReverseComparer();
                            Array.Sort(files, myComparer);
                            break;
                    }

                    if (slot.required && !files.Any())
                    {
                        throw new MissingRequiredInstanceFiles("Missing required files.");
                    }

                    foreach (string file in files)
                    {
                        if (File.GetAttributes(file).HasFlag(FileAttributes.Hidden))
                        {
                            continue;
                        }

                        SimpleDataSlot current = new();
                        string filename = Path.GetFileName(file);

                        if (slot.as_filename)
                        {
                            jsonFileName = Path.GetFileNameWithoutExtension(file) + ".json";
                        }

                        current.id = index.ToString();
                        current.filename = filename;
                        index++;
                        slots.Add(current);
                    }
                }

                var limit = (JsonElement)jsonPackager.slot_limit["count"];

                if (slots.Count == 0 || (jsonPackager.slot_limit != null && slots.Count > limit.GetInt32()))
                {
                    WriteMessage("Unable to build " + jsonFileName);
                    warning = true;
                    continue;
                }

                instance.data_slots = slots.ToArray();
                simpleInstanceJson.instance = instance;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string[] parts = dir.Split(commonPath);
                parts = parts[1].Split(jsonFileName.Remove(jsonFileName.Length - 5));
                string subDirectory = string.Empty;

                if (parts[0].Length > 1)
                {
                    subDirectory = parts[0].Trim(Path.DirectorySeparatorChar);
                }

                string outputFile = Path.Combine(GlobalHelper.UpdateDirectory, jsonPackager.output, subDirectory, jsonFileName);

                if (!overwrite && File.Exists(outputFile))
                {
                    WriteMessage(jsonFileName + " already exists.");
                }
                else
                {
                    string json = JsonSerializer.Serialize(simpleInstanceJson, options);

                    WriteMessage("Saving " + jsonFileName);

                    FileInfo file = new FileInfo(outputFile);

                    file.Directory.Create(); // If the directory already exists, this method does nothing.

                    File.WriteAllText(outputFile, json);
                }
            }
            catch (MissingRequiredInstanceFiles)
            {
                // Do nothing.
            }
            catch (Exception)
            {
                WriteMessage("Unable to build " + dirName);
            }
        }

        if (warning)
        {
            var message = (JsonElement)jsonPackager.slot_limit["message"];

            WriteMessage(message.GetString());
        }

        WriteMessage("Finished");
    }

    public bool CheckInstancePackager()
    {
        string instancePackagerFile = Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "instance-packager.json");

        return File.Exists(instancePackagerFile);
    }

    private DataJSON ReadDataJSON()
    {
        string updateDirectory = GlobalHelper.UpdateDirectory;
        string coreDirectory = Path.Combine(updateDirectory, "Cores", this.identifier);
        string dataFile = Path.Combine(coreDirectory, "data.json");
        var options = new JsonSerializerOptions { Converters = { new StringConverter() } };

        DataJSON data = JsonSerializer.Deserialize<DataJSON>(File.ReadAllText(dataFile), options);

        return data;
    }

    public bool JTBetaCheck()
    {
        var data = ReadDataJSON();
        bool check = data.data.data_slots.Any(x => x.name == "JTBETA");

        if (check)
        {
            var slot = data.data.data_slots.First(x => x.name == "JTBETA");

            this.beta_slot_id = slot.id;
            this.beta_slot_platform_id_index = slot.GetPlatformIdIndex();
        }

        return check;
    }

    public void ReplaceCheck()
    {
        var replaces = this.GetSubstitutes();

        if (replaces != null)
        {
            foreach (var replacement in replaces)
            {
                string identifier = $"{replacement.author}.{replacement.shortname}";
                Core c = new Core { identifier = identifier, platform_id = replacement.platform_id };

                if (c.IsInstalled())
                {
                    Replace(c);
                    c.Uninstall();
                    WriteMessage($"Uninstalled {identifier}. It was replaced by this core.");
                }
            }
        }
    }

    private void Replace(Core core)
    {
        string root = GlobalHelper.UpdateDirectory;
        string path = Path.Combine(root, "Assets", core.platform_id, core.identifier);

        if (Directory.Exists(path))
        {
            Directory.Move(path, Path.Combine(root, "Assets", core.platform_id, this.identifier));
        }

        path = Path.Combine(root, "Saves", core.platform_id, core.identifier);
        if (Directory.Exists(path))
        {
            Directory.Move(path, Path.Combine(root, "Saves", core.platform_id, this.identifier));
        }

        path = Path.Combine(root, "Settings", core.identifier);
        if (Directory.Exists(path))
        {
            Directory.Move(path, Path.Combine(root, "Settings", this.identifier));
        }
    }

    public Video GetVideoConfig()
    {
        CheckUpdateDirectory();

        string file = Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "video.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        var options = new JsonSerializerOptions { AllowTrailingCommas = true };
        Video config = JsonSerializer.Deserialize<Dictionary<string, Video>>(json, options)["video"];

        return config;
    }

    public void AddDisplayModes()
    {
        var info = this.GetConfig();
        var video = GetVideoConfig();
        List<DisplayMode> all = new List<DisplayMode>();

        foreach (string id in ALL_MODES)
        {
            all.Add(new DisplayMode { id = id });
        }

        if (info.metadata.platform_ids.Contains("gb"))
        {
            foreach (string id in GB_MODES)
            {
                all.Add(new DisplayMode { id = id });
            }
        }

        video.display_modes = all;

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(output, options);

        File.WriteAllText(Path.Combine(GlobalHelper.UpdateDirectory, "Cores", this.identifier, "video.json"), json);
    }
}

public class ReverseComparer : IComparer
{
    // Calls CaseInsensitiveComparer.Compare with the parameters reversed.
    int IComparer.Compare(object x, object y)
    {
        return new CaseInsensitiveComparer().Compare(y, x);
    }
}
