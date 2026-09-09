using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella.Services;

public partial class CoresService
{
    private const string LICENSE_EXTRACT_LOCATION = "Licenses";

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

    public void RetrieveKeys()
    {
        string keyPath = Path.Combine(this.installPath, LICENSE_EXTRACT_LOCATION);

        bool foundLocalJtBeta = this.ExtractJTBetaKey();

        if (!foundLocalJtBeta)
        {
            var config = ServiceHelper.SettingsService.Config;

            if (config.jt_beta_github_fetch || config.jt_beta_patreon_fetch)
            {
                this.AutoFetchJtBetaKey();
            }
        }

        if (ServiceHelper.SettingsService.Config.coin_op_beta)
        {
            string serial = null;
            var idFiles = Directory.GetFiles(this.installPath, "*.ID");

            if (idFiles.Length > 0)
            {
                serial = Path.GetFileNameWithoutExtension(idFiles[0]);
            }
            else
            {
                Console.WriteLine("Coin-Op Collection Beta is enabled, but no .ID file was found in the root of your SD card.");
                Console.WriteLine("To create one, paste your device serial number from the Coin-Op license portal.");
                Console.Write("Enter serial number (or leave blank to skip): ");

                string input = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(input))
                {
                    serial = input.Trim();
                    string idFilePath = Path.Combine(this.installPath, serial + ".ID");
                    File.Create(idFilePath).Dispose();
                    Console.WriteLine($"Created {serial}.ID");
                }
            }

            if (serial != null)
            {
                if (!Directory.Exists(keyPath))
                {
                    Directory.CreateDirectory(keyPath);
                }

                try
                {
                    Console.WriteLine("Retrieving Coin-Op Collection license...");

                    var license = CoinOpService.FetchLicense(serial);

                    File.WriteAllBytes(Path.Combine(keyPath, "coinop.key"), license);

                    Console.WriteLine("License successfully downloaded.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving Coin-Op Collection license: {ex.Message}");
                }
                finally
                {
                    Divide();
                }
            }
        }
    }
}
