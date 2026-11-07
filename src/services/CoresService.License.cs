using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella.Services;

public partial class CoresService
{
    private const string LICENSE_EXTRACT_LOCATION = "Licenses";
    private const string JTBETA_KEY_FILENAME = "jtbeta.zip";
    private const string JTBETA_KEY_ALT_FILENAME = "beta.bin";

    public (bool, string, int, string) RequiresLicense(string identifier)
    {
        var updater = this.ReadUpdatersJson(identifier);

        if (updater?.license == null)
        {
            return (false, null, 0, null);
        }

        var data = this.ReadDataJson(identifier);
        var slot = data.data.data_slots.FirstOrDefault(x => x.filename == updater.license.filename);

        return slot != null
            ? (true, slot.id, slot.GetPlatformIdIndex(), updater.license.filename)
            : (false, null, 0, null);
    }

    public void CopyLicense(Core core)
    {
        AnalogueCore info = this.ReadCoreJson(core.id);
        string path = Path.Combine(
            this.installPath,
            "Assets",
            info.metadata.platform_ids[core.license_slot_platform_id_index],
            "common");
        string licensePath = Path.Combine(this.installPath, LICENSE_EXTRACT_LOCATION);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        string keyFile = Path.Combine(licensePath, core.license_slot_filename);

        if (File.Exists(keyFile) && Directory.Exists(path))
        {
            File.Copy(keyFile, Path.Combine(path, core.license_slot_filename), true);
            WriteMessage($"License copied to '{path}'.");
        }
    }

    /// <summary>
    /// Copies a beta key the user placed on the SD card root (jtbeta.zip or beta.bin) into Licenses/.
    /// Fetching keys from the network is done by plugins.
    /// </summary>
    public bool ImportLocalLicenseFiles()
    {
        string keyPath = Path.Combine(this.installPath, LICENSE_EXTRACT_LOCATION);
        string zipFile = Path.Combine(this.installPath, JTBETA_KEY_FILENAME);

        if (File.Exists(zipFile))
        {
            WriteMessage("JT beta key detected. Extracting...");
            ZipHelper.ExtractToDirectory(zipFile, keyPath, true, false);

            return true;
        }

        string binFile = Path.Combine(this.installPath, JTBETA_KEY_ALT_FILENAME);

        if (File.Exists(binFile))
        {
            WriteMessage("JT beta key detected.");

            if (!Directory.Exists(keyPath))
            {
                Directory.CreateDirectory(keyPath);
            }

            File.Copy(binFile, Path.Combine(keyPath, JTBETA_KEY_ALT_FILENAME), true);

            return true;
        }

        return false;
    }
}
