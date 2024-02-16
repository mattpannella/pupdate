using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Analogue.Data;
using Pannella.Models.Analogue.Instance;
using Pannella.Models.Analogue.Instance.Simple;
using Pannella.Models.Analogue.Video;
using Pannella.Models.Extras;
using Pannella.Models.InstancePackager;
using Pannella.Models.Updater;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;
using ArchiveFile = Pannella.Models.Archive.File;
using DataSlot = Pannella.Models.Analogue.Shared.DataSlot;
using InstancePackagerDataSlot =  Pannella.Models.InstancePackager.DataSlot;

namespace Pannella.Models;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
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
            await this.PocketExtraCheck();

            return true;
        }

        return false;
    }

    private async Task PocketExtraCheck()
    {
        var coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(this.identifier);

        if (coreSettings.pocket_extras)
        {
            PocketExtra pocketExtra = GlobalHelper.GetPocketExtra(this.identifier);

            if (pocketExtra != null)
            {
                WriteMessage("Reapplying Pocket Extras...");
                await GlobalHelper.PocketExtrasService.GetPocketExtra(pocketExtra, GlobalHelper.UpdateDirectory, false);
            }
        }
    }

    private async Task<bool> InstallGithubAsset(bool preservePlatformsFolder)
    {
        if (this.download_url == null)
        {
            WriteMessage("No release URL found...");

            return false;
        }

        WriteMessage($"Downloading file {this.download_url}...");

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

        if (!GlobalHelper.SettingsManager.GetConfig().download_assets ||
            !GlobalHelper.SettingsManager.GetCoreSettings(this.identifier).download_assets)
        {
            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingBetaKey", false }
            };
        }

        CheckUpdateDirectory();
        WriteMessage("Looking for Assets...");
        AnalogueCore info = this.GetConfig();
        string updateDirectory = GlobalHelper.UpdateDirectory;
        // cores with multiple platforms won't work...not sure any exist right now?
        string instancesDirectory = Path.Combine(updateDirectory, "Assets", info.metadata.platform_ids[0], this.identifier);
        string platformPath = Path.Combine(updateDirectory, "Assets", info.metadata.platform_ids[0]);
        string path;
        var options = new JsonSerializerOptions { Converters = { new StringConverter() } };

        DataJSON dataJson = ReadDataJSON();

        if (dataJson.data.data_slots.Length > 0)
        {
            foreach (DataSlot slot in dataJson.data.data_slots)
            {
                if (slot.filename != null && !slot.filename.EndsWith(".sav") &&
                    !GlobalHelper.Blacklist.Contains(slot.filename))
                {
                    if (slot.IsCoreSpecific())
                    {
                        path = Path.Combine(platformPath, this.identifier);
                    }
                    else
                    {
                        path = Path.Combine(platformPath, "common");

                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);
                    }

                    List<string> files = new List<string> { slot.filename };

                    if (slot.alternate_filenames != null)
                    {
                        files.AddRange(slot.alternate_filenames.Where(f => !GlobalHelper.Blacklist.Contains(f)));
                    }

                    foreach (string f in files)
                    {
                        string filepath = Path.Combine(path, f);

                        if (File.Exists(filepath) && CheckCRC(filepath, GlobalHelper.ArchiveFiles))
                        {
                            WriteMessage($"Already installed: {f}");
                        }
                        else
                        {
                            bool result = await DownloadAsset(
                                f,
                                filepath,
                                GlobalHelper.ArchiveFiles,
                                GlobalHelper.SettingsManager.GetConfig().archive_name,
                                GlobalHelper.SettingsManager.GetConfig().use_custom_archive);

                            if (result)
                            {
                                installed.Add(filepath.Replace(updateDirectory, string.Empty));
                            }
                            else
                            {
                                skipped.Add(filepath.Replace(updateDirectory, string.Empty));
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

        if (this.identifier is "agg23.GameAndWatch" && GlobalHelper.SettingsManager.GetConfig().download_gnw_roms)
        {
            string commonPath = Path.Combine(platformPath, "common");

            if (!Directory.Exists(commonPath))
                Directory.CreateDirectory(commonPath);

            foreach (var f in GlobalHelper.GameAndWatchArchiveFiles.files)
            {
                string filePath = Path.Combine(commonPath, f.name);
                string subDirectory = Path.GetDirectoryName(f.name);

                if (!string.IsNullOrEmpty(subDirectory))
                {
                    Directory.CreateDirectory(Path.Combine(commonPath, subDirectory));
                }

                if (File.Exists(filePath) && CheckCRC(filePath, GlobalHelper.GameAndWatchArchiveFiles))
                {
                    WriteMessage($"Already installed: {f.name}");
                }
                else
                {
                    bool result = await DownloadAsset(
                        f.name,
                        filePath,
                        GlobalHelper.GameAndWatchArchiveFiles,
                        GlobalHelper.SettingsManager.GetConfig().gnw_archive_name);

                    if (result)
                    {
                        installed.Add(filePath.Replace(updateDirectory, string.Empty));
                    }
                    else
                    {
                        skipped.Add(filePath.Replace(updateDirectory, string.Empty));
                    }
                }
            }

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

                            if (!GlobalHelper.Blacklist.Contains(slot.filename) && !slot.filename.EndsWith(".sav"))
                            {
                                string commonPath = Path.Combine(platformPath, "common");

                                if (!Directory.Exists(commonPath))
                                    Directory.CreateDirectory(commonPath);

                                string slotPath = Path.Combine(commonPath, dataPath, slot.filename);

                                if (File.Exists(slotPath) && CheckCRC(slotPath, GlobalHelper.ArchiveFiles))
                                {
                                    WriteMessage($"Already installed: {slot.filename}");
                                }
                                else
                                {
                                    bool result = await DownloadAsset(
                                        slot.filename,
                                        slotPath,
                                        GlobalHelper.ArchiveFiles,
                                        GlobalHelper.SettingsManager.GetConfig().archive_name,
                                        GlobalHelper.SettingsManager.GetConfig().use_custom_archive);

                                    if (result)
                                    {
                                        installed.Add(slotPath.Replace(updateDirectory, string.Empty));
                                    }
                                    else
                                    {
                                        skipped.Add(slotPath.Replace(updateDirectory, string.Empty));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    WriteMessage($"Error while processing '{file}'");
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

    private async Task<bool> DownloadAsset(string fileName, string destination, Archive.Archive archive,
        string archiveName, bool useCustomArchive = false)
    {
        if (archive != null)
        {
            ArchiveFile file = archive.GetFile(fileName);

            if (file == null)
            {
                WriteMessage($"Unable to find '{fileName}' in archive");
                return false;
            }
        }

        try
        {
            string url;

            if (useCustomArchive)
            {
                var custom = GlobalHelper.SettingsManager.GetConfig().custom_archive;
                Uri baseUri = new Uri(custom["url"]);
                Uri uri = new Uri(baseUri, fileName);

                url = uri.ToString();
            }
            else
            {
                url = $"{ARCHIVE_BASE_URL}/{archiveName}/{fileName}";
            }

            int count = 0;

            do
            {
                WriteMessage($"Downloading '{fileName}'");
                await HttpHelper.Instance.DownloadFileAsync(url, destination, 600);
                WriteMessage($"Finished downloading '{fileName}'");
                count++;
            }
            while (count < 3 && !CheckCRC(destination, GlobalHelper.ArchiveFiles));
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                WriteMessage($"Unable to find '{fileName}' in archive");
            }
            else
            {
                WriteMessage($"There was a problem downloading '{fileName}'");
            }

            return false;
        }
        catch (Exception e)
        {
            WriteMessage($"Something went wrong with '{fileName}'");
            WriteMessage(e.ToString());

            return false;
        }

        return true;
    }

    private bool CheckCRC(string filepath, Archive.Archive archive)
    {
        if (archive == null || !GlobalHelper.SettingsManager.GetConfig().crc_check)
        {
            return true;
        }

        string filename = Path.GetFileName(filepath);
        ArchiveFile file = archive.GetFile(filename);

        if (file == null)
        {
            return true; // no checksum to compare to
        }

        if (Util.CompareChecksum(filepath, file.crc32))
        {
            return true;
        }

        WriteMessage($"{filename}: Bad checksum!");
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
                instance.data_path = dir.Replace(commonPath + Path.DirectorySeparatorChar, string.Empty) + "/";

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
                    WriteMessage($"Unable to build {jsonFileName}");
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
                    WriteMessage($"{jsonFileName} already exists.");
                }
                else
                {
                    string json = JsonSerializer.Serialize(simpleInstanceJson, options);

                    WriteMessage($"Saving {jsonFileName}");

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
                WriteMessage($"Unable to build {dirName}");
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
