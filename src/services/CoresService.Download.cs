using System.Collections;
using Newtonsoft.Json;
using Pannella.Exceptions;
using Pannella.Helpers;
using Pannella.Models.Analogue.Data;
using Pannella.Models.Analogue.Instance;
using Pannella.Models.Analogue.Instance.Simple;
using Pannella.Models.Events;
using Pannella.Models.InstancePackager;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Models.Settings;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;
using ArchiveFile = Pannella.Models.Archive.File;
using DataSlot = Pannella.Models.Analogue.Shared.DataSlot;
using File = System.IO.File;
using InstancePackagerDataSlot =  Pannella.Models.InstancePackager.DataSlot;

namespace Pannella.Services;

public partial class CoresService
{
    public void DownloadCoreAssets(List<Core> coreList)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingLicenses = new List<string>();

        if (coreList == null)
        {
            WriteMessage("List of cores is required.");
            return;
        }

        foreach (var core in coreList)
        {
            try
            {
                string name = core.identifier;

                if (name == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage(core.identifier);

                var results = this.DownloadAssets(core, true);

                installedAssets.AddRange((List<string>)results["installed"]);
                skippedAssets.AddRange((List<string>)results["skipped"]);

                if ((bool)results["missingLicense"])
                {
                    missingLicenses.Add(core.identifier);
                }

                Divide();
            }
            catch (Exception e)
            {
                WriteMessage("Uh oh something went wrong.");
#if DEBUG
                WriteMessage(e.ToString());
#else
                WriteMessage(e.Message);
#endif
            }
        }

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "All Done",
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingLicenses = missingLicenses,
            SkipOutro = false,
        };

        OnUpdateProcessComplete(args);
    }

    public Dictionary<string, object> DownloadAssets(Core core, bool ignoreGlobalSetting = false)
    {
        List<string> installed = new List<string>();
        List<string> skipped = new List<string>();
        bool missingLicense = false;

        // Should this just check the Installed Cores collection instead?
        if (!this.IsInstalled(core.identifier))
        {
            WriteMessage($"Core '{core.identifier}' is not installed yet.");

            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingLicense", false }
            };
        }

        //dynamically add the license file to the blacklist so we dont try to download it
        if (core.license_slot_filename != null)
        {
            this.assetsService.Blacklist.Add(core.license_slot_filename);
        }

        // run if:
        // global override is on and core specific is on
        // or
        // global override is off, global setting is on, and core specific is on
        bool run = (ignoreGlobalSetting && this.settingsService.GetCoreSettings(core.identifier).download_assets) ||
                   (!ignoreGlobalSetting && this.settingsService.GetConfig().download_assets &&
                    this.settingsService.GetCoreSettings(core.identifier).download_assets);

        if (!run)
        {
            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingLicense", false }
            };
        }

        WriteMessage("Looking for Assets...");
        Archive archive = this.archiveService.GetArchive(core.identifier);
        AnalogueCore info = this.ReadCoreJson(core.identifier);
        // cores with multiple platforms won't work...not sure any exist right now?
        string platformPath = Path.Combine(this.installPath, "Assets", info.metadata.platform_ids[0]);

        DataJSON dataJson = this.ReadDataJson(core.identifier);

        if (dataJson.data.data_slots.Length > 0)
        {
            foreach (DataSlot slot in dataJson.data.data_slots)
            {
                if (!string.IsNullOrEmpty(slot.filename) &&
                    !slot.filename.EndsWith(".sav") &&
                    !this.assetsService.Blacklist.Contains(slot.filename))
                {
                    string path;

                    if (slot.IsCoreSpecific())
                    {
                        path = Path.Combine(platformPath, core.identifier);
                    }
                    else
                    {
                        path = Path.Combine(platformPath, "common");

                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);
                    }

                    List<string> files = new() { slot.filename };

                    if (slot.alternate_filenames != null)
                    {
                        files.AddRange(slot.alternate_filenames.Where(f => !this.assetsService.Blacklist.Contains(f)));
                    }

                    foreach (string file in files)
                    {
                        string filePath = Path.Combine(path, file);
                        ArchiveFile archiveFile = this.archiveService.GetArchiveFile(file, core.identifier);

                        if (File.Exists(filePath) && CheckCrc(filePath, archiveFile))
                        {
                            if (!this.settingsService.GetConfig().suppress_already_installed)
                                WriteMessage($"Already installed: {file}");
                        }
                        else
                        {
                            WriteMessage($"Downloading: {slot.filename}...");
                            bool result = this.archiveService.DownloadArchiveFile(archive, archiveFile, path);

                            if (result)
                            {
                                WriteMessage($"Installed: {slot.filename}");
                                installed.Add(filePath.Replace(this.installPath, string.Empty));
                            }
                            else
                            {
                                WriteMessage($"Not found: {slot.filename}");
                                skipped.Add(filePath.Replace(this.installPath, string.Empty));
                            }
                        }
                    }
                }
            }
        }

        if (archive.type == ArchiveType.core_specific_archive && archive.enabled && !archive.has_instance_jsons)
        {
            var files = this.archiveService.GetArchiveFiles(archive);

            string commonPath = Path.Combine(platformPath, "common");

            Directory.CreateDirectory(commonPath);

            foreach (var file in files)
            {
                string filePath = Path.Combine(commonPath, file.name);

                if (File.Exists(filePath) && CheckCrc(filePath, file))
                {
                    if (!this.settingsService.GetConfig().suppress_already_installed)
                        WriteMessage($"Already installed: {file.name}");
                }
                else
                {
                    WriteMessage($"Downloading: {file.name}...");
                    bool result = this.archiveService.DownloadArchiveFile(archive, file, commonPath);

                    if (result)
                    {
                        WriteMessage($"Installed: {file.name}");
                        installed.Add(filePath.Replace(this.installPath, string.Empty));
                    }
                    else
                    {
                        WriteMessage($"Not found: {file.name}");
                        skipped.Add(filePath.Replace(this.installPath, string.Empty));
                    }
                }
            }
        }

        // These cores have instance json files and the roms are not in the default archive.
        // Check to see if they have a core specific archive defined, skip otherwise.
        if (this.IgnoreInstanceJson.Contains(core.identifier) && archive.type != ArchiveType.core_specific_archive)
        {
            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingLicense", false }
            };
        }

        if (CheckInstancePackager(core.identifier))
        {
            this.BuildInstanceJson(core.identifier);

            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingLicense", false }
            };
        }

        string instancesDirectory = Path.Combine(this.installPath, "Assets", info.metadata.platform_ids[0], core.identifier);

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

                    if (this.settingsService.GetConfig().skip_alternative_assets &&
                        file.Contains(Path.Combine(instancesDirectory, "_alternatives")))
                    {
                        continue;
                    }

                    InstanceJSON instanceJson = JsonConvert.DeserializeObject<InstanceJSON>(File.ReadAllText(file));

                    if (instanceJson.instance.data_slots is { Length: > 0 })
                    {
                        string dataPath = instanceJson.instance.data_path;

                        foreach (DataSlot slot in instanceJson.instance.data_slots)
                        {
                            var platformId = info.metadata.platform_ids[core.license_slot_platform_id_index];

                            if (!CheckLicenseMd5(slot, core.license_slot_id, platformId))
                            {
                                // Moved message to the CheckBetaMd5 method
                                missingLicense = true;
                            }

                            if (!this.assetsService.Blacklist.Contains(slot.filename) &&
                                !slot.filename.EndsWith(".sav"))
                            {
                                string commonPath = Path.Combine(platformPath, "common");

                                if (!Directory.Exists(commonPath))
                                    Directory.CreateDirectory(commonPath);

                                string slotDirectory = Path.Combine(commonPath, dataPath);
                                string slotPath = Path.Combine(slotDirectory, slot.filename);
                                ArchiveFile archiveFile = this.archiveService.GetArchiveFile(slot.filename, core.identifier);

                                if (File.Exists(slotPath) && CheckCrc(slotPath, archiveFile))
                                {
                                    if (!this.settingsService.GetConfig().suppress_already_installed)
                                        WriteMessage($"Already installed: {slot.filename}");
                                }
                                else
                                {
                                    WriteMessage($"Downloading: {slot.filename}...");
                                    bool result = this.archiveService.DownloadArchiveFile(archive, archiveFile, slotDirectory);

                                    if (result)
                                    {
                                        WriteMessage($"Installed: {slot.filename}");
                                        installed.Add(slotPath.Replace(this.installPath, string.Empty));
                                    }
                                    else
                                    {
                                        WriteMessage($"Not found: {slot.filename}");
                                        skipped.Add(slotPath.Replace(this.installPath, string.Empty));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    WriteMessage($"Error while processing '{file}'");
#if DEBUG
                    WriteMessage(e.ToString());
#else
                    WriteMessage(e.Message);
#endif
                }
            }
        }

        Dictionary<string, object> results = new Dictionary<string, object>
        {
            { "installed", installed },
            { "skipped", skipped },
            { "missingLicense", missingLicense }
        };

        return results;
    }

    public void BuildInstanceJson(string identifier, bool overwrite = true)
    {
        if (!this.settingsService.GetConfig().build_instance_jsons)
        {
            return;
        }

        string instancePackagerFile = Path.Combine(this.installPath, "Cores", identifier, "instance-packager.json");

        if (!File.Exists(instancePackagerFile))
        {
            return;
        }

        WriteMessage("Building instance json files.");
        InstanceJsonPackager jsonPackager = JsonConvert.DeserializeObject<InstanceJsonPackager>(File.ReadAllText(instancePackagerFile));
        string commonPath = Path.Combine(this.installPath, "Assets", jsonPackager.platform_id, "common");
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

                var limit = (long)jsonPackager.slot_limit["count"];

                if (slots.Count == 0 || (jsonPackager.slot_limit != null && slots.Count > limit))
                {
                    WriteMessage($"Unable to build {jsonFileName}");
                    warning = true;
                    continue;
                }

                instance.data_slots = slots.ToArray();
                simpleInstanceJson.instance = instance;

                string[] parts = dir.Split(commonPath);
                // split on dir separator and remove last one?
                parts = parts[1].Split(dirName);
                string subDirectory = string.Empty;

                if (parts[0].Length > 1)
                {
                    subDirectory = parts[0].Trim(Path.DirectorySeparatorChar);
                }

                string outputFile = Path.Combine(this.installPath, jsonPackager.output, subDirectory, jsonFileName);

                if (!overwrite && File.Exists(outputFile))
                {
                    WriteMessage($"{jsonFileName} already exists.");
                }
                else
                {
                    string json = JsonConvert.SerializeObject(simpleInstanceJson, Formatting.Indented);

                    WriteMessage($"Saving '{jsonFileName}'...");

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
            var message = (string)jsonPackager.slot_limit["message"];

            WriteMessage(message);
        }

        WriteMessage("Finished");
    }

    public bool CheckInstancePackager(string identifier)
    {
        string instancePackagerFile = Path.Combine(this.installPath, "Cores", identifier, "instance-packager.json");

        return File.Exists(instancePackagerFile);
    }
}
