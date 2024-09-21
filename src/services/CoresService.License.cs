using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;
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
        AnalogueCore info = this.ReadCoreJson(core.identifier);
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

    public bool RetrieveKeys()
    {
        string keyPath = Path.Combine(this.installPath, LICENSE_EXTRACT_LOCATION);
        this.ExtractJTBetaKey();

        string email = ServiceHelper.SettingsService.GetConfig().patreon_email_address;
        if (email == null && ServiceHelper.SettingsService.GetConfig().coin_op_beta)
        {
            Console.WriteLine("Unable to retrieve Coin-Op Collection Beta license. Please set your patreon email address.");
            Console.Write("Enter value: ");
            email = Console.ReadLine();
            ServiceHelper.SettingsService.GetConfig().patreon_email_address = email;
            ServiceHelper.SettingsService.Save();
        }
        if (email != null && ServiceHelper.SettingsService.GetConfig().coin_op_beta)
        {
            if (!Directory.Exists(keyPath))
            {
                Directory.CreateDirectory(keyPath);
            }
            try {
                Console.WriteLine("Retrieving Coin-Op Collection license...");
                var license = CoinOpService.FetchLicense(email);
                File.WriteAllBytes(Path.Combine(keyPath, "coinop.key"), license);
                Console.WriteLine("License successfully downloaded.");
            } catch (Exception e) {
                Console.WriteLine(e.Message);
            } finally {
                Divide();
            }
        }

        return true;
    }
}
