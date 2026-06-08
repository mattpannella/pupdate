using System.Collections;
using Newtonsoft.Json;
using Pannella.Exceptions;
using Pannella.Helpers;
using Pannella.Models.Analogue.Data;
using Pannella.Models.Analogue.Instance;
using Pannella.Models.Analogue.Instance.Simple;
using Pannella.Models.Events;
using Pannella.Models.InstancePackager;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
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
                string name = core.id;

                if (name == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage(core.id);

                var results = this.DownloadAssets(core, true);

                installedAssets.AddRange((List<string>)results["installed"]);
                skippedAssets.AddRange((List<string>)results["skipped"]);

                if ((bool)results["missingLicense"])
                {
                    missingLicenses.Add(core.id);
                }

                Divide();
            }
            catch (Exception ex)
            {
                WriteMessage("Uh oh something went wrong.");
                WriteMessage(this.settingsService.Debug.show_stack_traces
                    ? ex.ToString()
                    : Util.GetExceptionMessage(ex));
            }
        }

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "All Done",
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingLicenses = missingLicenses,
            SkipOutro = false
        };

        OnUpdateProcessComplete(args);
    }

    // A single asset file to download: the archive entry, the directory it goes
    // into, the full path it lands at, and the name shown in status messages.
    private readonly record struct AssetDownload(
        ArchiveFile ArchiveFile, string DestinationDirectory, string FilePath, string DisplayName);

    // Downloads a batch of asset files. The network transfers run concurrently
    // (bounded by the concurrent_downloads setting); extraction and the
    // installed/skipped bookkeeping run serially afterwards, since extracting
    // into a shared directory could collide and those lists are not thread-safe.
    private bool DownloadAssetBatch(Archive archive, List<AssetDownload> batch, List<string> installed,
        List<string> skipped, bool extractArchives)
    {
        bool allSucceeded = true;

        if (batch.Count == 0)
        {
            return allSucceeded;
        }

        int maxParallel = Math.Max(1, this.settingsService.Config.concurrent_downloads);
        bool[] results = new bool[batch.Count];

        if (maxParallel > 1)
        {
            WriteMessage($"Downloading {batch.Count} file(s), up to {maxParallel} at a time...");

            // Each task writes a distinct file and a distinct results slot, so no
            // shared mutable state is touched. Progress bars are suppressed because
            // concurrent bars would corrupt the single console line.
            bool previousSuppress = HttpHelper.Instance.SuppressProgressBar;
            HttpHelper.Instance.SuppressProgressBar = true;

            try
            {
                Parallel.For(0, batch.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = maxParallel },
                    i => results[i] = this.archiveService.DownloadArchiveFile(
                        archive, batch[i].ArchiveFile, batch[i].DestinationDirectory));
            }
            finally
            {
                HttpHelper.Instance.SuppressProgressBar = previousSuppress;
            }
        }
        else
        {
            for (int i = 0; i < batch.Count; i++)
            {
                WriteMessage($"Downloading: {batch[i].DisplayName}...");
                results[i] = this.archiveService.DownloadArchiveFile(
                    archive, batch[i].ArchiveFile, batch[i].DestinationDirectory);
            }
        }

        for (int i = 0; i < batch.Count; i++)
        {
            AssetDownload item = batch[i];

            if (extractArchives && results[i] && File.Exists(item.FilePath))
            {
                string extension = Path.GetExtension(item.FilePath);

                if (extension == ".zip")
                {
                    ZipHelper.ExtractToDirectory(item.FilePath, Path.GetDirectoryName(item.FilePath), true);
                    File.Delete(item.FilePath);
                }
                else if (extension == ".7z")
                {
                    SevenZipHelper.ExtractToDirectory(item.FilePath, Path.GetDirectoryName(item.FilePath));
                    File.Delete(item.FilePath);
                }
            }

            if (results[i])
            {
                WriteMessage($"Installed: {item.DisplayName}");
                installed.Add(item.FilePath.Replace(this.installPath, string.Empty));
            }
            else
            {
                WriteMessage($"Not found: {item.DisplayName}");
                skipped.Add(item.FilePath.Replace(this.installPath, string.Empty));
                allSucceeded = false;
            }
        }

        return allSucceeded;
    }

    public Dictionary<string, object> DownloadAssets(Core core, bool ignoreGlobalSetting = false)
    {
        List<string> installed = new List<string>();
        List<string> skipped = new List<string>();
        bool missingLicense = false;

        // Should this just check the Installed Cores collection instead?
        if (!this.IsInstalled(core.id))
        {
            WriteMessage($"Core '{core.id}' is not installed yet.");

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
        bool run = (ignoreGlobalSetting && this.settingsService.GetCoreSettings(core.id).download_assets) ||
                   (!ignoreGlobalSetting && this.settingsService.Config.download_assets &&
                    this.settingsService.GetCoreSettings(core.id).download_assets);

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

        Archive archive = this.archiveService.GetArchive();
        AnalogueCore info = this.ReadCoreJson(core.id);
        // cores with multiple platforms won't work...not sure any exist right now?
        string platformPath = Path.Combine(this.installPath, "Assets", info.metadata.platform_ids[0]);

        DataJSON dataJson = this.ReadDataJson(core.id);

        if (dataJson.data.data_slots.Length > 0)
        {
            // Collect the files needing download across all data slots so the
            // network transfers can be batched and run concurrently.
            List<AssetDownload> batch = new List<AssetDownload>();

            foreach (DataSlot slot in dataJson.data.data_slots)
            {
                if (!string.IsNullOrEmpty(slot.filename) &&
                    !slot.filename.EndsWith(".sav") &&
                    !this.assetsService.IsBlacklisted(slot.filename))
                {
                    string path;

                    if (slot.IsCoreSpecific())
                    {
                        path = Path.Combine(platformPath, core.id);
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
                        files.AddRange(slot.alternate_filenames.Where(f => !this.assetsService.IsBlacklisted(f)));
                    }

                    foreach (string file in files)
                    {
                        string filePath = Path.Combine(path, file);
                        ArchiveFile archiveFile = this.archiveService.GetArchiveFile(file);

                        if (File.Exists(filePath) && CheckCrc(filePath, archiveFile))
                        {
                            if (!this.settingsService.Config.suppress_already_installed)
                                WriteMessage($"Already installed: {file}");
                        }
                        else
                        {
                            batch.Add(new AssetDownload(archiveFile, path, filePath, slot.filename));
                        }
                    }
                }
            }

            DownloadAssetBatch(archive, batch, installed, skipped, extractArchives: false);
        }
        
        // grab the core specific archive, now
        archive = this.archiveService.GetArchive(core.id);

        if (archive.type is ArchiveType.core_specific_archive or ArchiveType.core_specific_custom_archive 
            && archive.enabled && !archive.has_instance_jsons
            && ((archive.one_time && !archive.complete) || !archive.one_time))
        {
            List<ArchiveFile> files = this.archiveService.GetArchiveFiles(archive).ToList();

            string commonPath = Path.Combine(platformPath, "common");

            Directory.CreateDirectory(commonPath);

            // Work out which files actually need downloading (serial: CheckCrc reads
            // each existing file and is cheap relative to the network transfers).
            List<AssetDownload> batch = new List<AssetDownload>();

            foreach (var file in files)
            {
                string filePath = Path.Combine(commonPath, file.name);

                if (File.Exists(filePath) && CheckCrc(filePath, file))
                {
                    if (!this.settingsService.Config.suppress_already_installed)
                        WriteMessage($"Already installed: {file.name}");
                }
                else
                {
                    batch.Add(new AssetDownload(file, commonPath, filePath, file.name));
                }
            }

            bool allSucceeded = DownloadAssetBatch(archive, batch, installed, skipped, extractArchives: true);

            if (archive.one_time && allSucceeded)
            {
                archive.complete = true;
                this.settingsService.Save();
            }
        }

        // These cores have instance json files and the roms are not in the default archive.
        // Check to see if they have a core specific archive defined, skip otherwise.
        if (this.IgnoreInstanceJson.Contains(core.id) && archive.type != ArchiveType.core_specific_archive)
        {
            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingLicense", false }
            };
        }

        if (CheckInstancePackager(core.id))
        {
            this.BuildInstanceJson(core.id);

            return new Dictionary<string, object>
            {
                { "installed", installed },
                { "skipped", skipped },
                { "missingLicense", false }
            };
        }

        string instancesDirectory = Path.Combine(this.installPath, "Assets", info.metadata.platform_ids[0], core.id);

        var instanceJsonBlocklist = this.settingsService.GetCoreSettings(core.id).instance_json_blocklist;

        if (Directory.Exists(instancesDirectory) && instanceJsonBlocklist != null)
        {
            foreach (string entry in instanceJsonBlocklist)
            {
                string blockedFileName = Path.GetFileNameWithoutExtension(entry) + ".json";

                foreach (string blockedFile in Directory.GetFiles(instancesDirectory, blockedFileName, SearchOption.AllDirectories))
                {
                    File.Delete(blockedFile);
                    WriteMessage($"Deleted blocked instance JSON: {Path.GetFileNameWithoutExtension(entry)}");
                }
            }
        }

        if (Directory.Exists(instancesDirectory))
        {
            string[] files = Directory.GetFiles(instancesDirectory, "*.json", SearchOption.AllDirectories);

            // Collect all instance-json ROM downloads so they can run concurrently.
            List<AssetDownload> instanceBatch = new List<AssetDownload>();

            foreach (string file in files)
            {
                try
                {
                    // skip mac ._ files
                    if (File.GetAttributes(file).HasFlag(FileAttributes.Hidden))
                    {
                        continue;
                    }

                    if (this.settingsService.Config.skip_alternative_assets &&
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
                            var fileName = Path.GetFileName(slot.filename);
                            var filePath = Path.GetDirectoryName(slot.filename) ?? string.Empty;

                            if (!CheckLicenseMd5(slot, core.license_slot_id, platformId))
                            {
                                // Moved message to the CheckBetaMd5 method
                                missingLicense = true;
                            }

                            if (!this.assetsService.IsBlacklisted(fileName) &&
                                !fileName.EndsWith(".sav"))
                            {
                                string commonPath = Path.Combine(platformPath, "common");

                                if (!Directory.Exists(commonPath))
                                    Directory.CreateDirectory(commonPath);

                                string slotDirectory = Path.Combine(commonPath, filePath);
                                if (!Directory.Exists(slotDirectory))
                                    Directory.CreateDirectory(slotDirectory);
                                string slotPath = Path.Combine(slotDirectory, fileName);
                                ArchiveFile archiveFile = this.archiveService.GetArchiveFile(fileName);

                                if (File.Exists(slotPath) && CheckCrc(slotPath, archiveFile))
                                {
                                    if (!this.settingsService.Config.suppress_already_installed)
                                        WriteMessage($"Already installed: {slot.filename}");
                                }
                                else
                                {
                                    instanceBatch.Add(new AssetDownload(archiveFile, slotDirectory, slotPath, slot.filename));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteMessage($"Error while processing '{file}'");
                    WriteMessage(this.settingsService.Debug.show_stack_traces
                        ? ex.ToString()
                        : Util.GetExceptionMessage(ex));
                }
            }

            DownloadAssetBatch(archive, instanceBatch, installed, skipped, extractArchives: false);
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
        if (!this.settingsService.Config.build_instance_jsons)
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
