using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella.Services;

public partial class CoresService
{
    private const string EXTRACT_LOCATION = "betakeys";

    public (bool, string, int) IsBetaCore(string identifier)
    {
        var updater = this.ReadUpdatersJson(identifier);
        if (updater.license == null) {
            return (false, null, 0);
        }

        var data = this.ReadDataJson(identifier);
        var slot = data.data.data_slots.FirstOrDefault(x => x.filename == updater.license.filename);

        return slot != null
            ? (true, slot.id, slot.GetPlatformIdIndex())
            : (false, null, 0);
    }

    public void CopyBetaKey(Core core)
    {
        AnalogueCore info = this.ReadCoreJson(core.identifier);
        string path = Path.Combine(
            this.installPath,
            "Assets",
            info.metadata.platform_ids[core.beta_slot_platform_id_index],
            "common");

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        string keyPath = Path.Combine(this.installPath, EXTRACT_LOCATION);

        if (Directory.Exists(keyPath) && Directory.Exists(path))
        {
            Util.CopyDirectory(keyPath, path, false, true);
            WriteMessage($"License copied to '{path}'.");
        }
    }

    public bool ExtractBetaKey(Core core)
    {
        string keyPath = Path.Combine(this.installPath, EXTRACT_LOCATION);
        string zipFile = Path.Combine(this.installPath, BETA_KEY_FILENAME);

        if (File.Exists(zipFile))
        {
            WriteMessage("License detected. Extracting...");
            ZipHelper.ExtractToDirectory(zipFile, keyPath, true);

            return true;
        }

        string binFile = Path.Combine(this.installPath, BETA_KEY_ALT_FILENAME);

        if (File.Exists(binFile))
        {
            WriteMessage("License detected.");

            if (!Directory.Exists(keyPath))
            {
                Directory.CreateDirectory(keyPath);
            }

            File.Copy(binFile, Path.Combine(keyPath, BETA_KEY_ALT_FILENAME), true);

            return true;
        }

        WriteMessage("License not found at either location:");
        WriteMessage($"     {zipFile}");
        WriteMessage($"     {binFile}");

        return false;
    }

    public void DeleteBetaKey()
    {
        string keyPath = Path.Combine(this.installPath, EXTRACT_LOCATION);

        if (Directory.Exists(keyPath))
            Directory.Delete(keyPath, true);
    }
}
