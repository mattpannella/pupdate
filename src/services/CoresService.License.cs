using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella.Services;

public partial class CoresService
{
    private const string LICENSE_EXTRACT_LOCATION = "Licenses";
    private const string COINOP_KEY_FILENAME = "coinop.key";
    private const string COINOP_ID_EXTENSION = ".ID";

    public static Func<string> CoinOpSerialPrompt;

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
            this.AutoFetchCoinOpKey();
        }
    }

    private void AutoFetchCoinOpKey()
    {
        (string serial, bool prompted) = this.ResolveCoinOpSerial();

        if (serial == null)
        {
            return;
        }

        try
        {
            WriteMessage("Retrieving Coin-Op Collection license...");

            byte[] license = CoinOpService.FetchLicense(serial);
            string keyPath = Path.Combine(this.installPath, LICENSE_EXTRACT_LOCATION);

            if (!Directory.Exists(keyPath))
            {
                Directory.CreateDirectory(keyPath);
            }

            File.WriteAllBytes(Path.Combine(keyPath, COINOP_KEY_FILENAME), license);

            WriteMessage($"Coin-Op Collection license downloaded ({license.Length:N0} bytes).");

            // Only remember a hand-typed serial once it's known good, so a typo doesn't leave a
            // dead .ID file behind that every later run picks up instead of re-prompting.
            if (prompted)
            {
                this.SaveCoinOpIdFile(serial);
            }
        }
        catch (Exception ex)
        {
            WriteMessage("Error retrieving Coin-Op Collection license: " + ex.Message);
        }
        finally
        {
            Divide();
        }
    }

    /// <summary>
    /// Finds the Coin-Op device serial, asking the active UI for it if there's no *.ID file yet.
    /// Returns the serial (null if unavailable) and whether it still needs saving to disk.
    /// </summary>
    private (string, bool) ResolveCoinOpSerial()
    {
        string fromFile = this.FindCoinOpIdFile();

        if (fromFile != null)
        {
            return (fromFile, false);
        }

        var prompt = CoinOpSerialPrompt;

        if (prompt == null)
        {
            WriteMessage("Coin-Op Collection beta access is enabled, but no .ID file was found in the root of " +
                         $"'{this.installPath}'. Create an empty file named <serial>{COINOP_ID_EXTENSION} using " +
                         "the device serial from the Coin-Op license portal, or run pupdate interactively to " +
                         "enter it.");
            Divide();

            return (null, false);
        }

        string serial = prompt()?.Trim();

        if (string.IsNullOrEmpty(serial))
        {
            return (null, false);
        }

        // Keeps a hand-typed serial safe to drop into a URL and a filename. The API rejects
        // anything that isn't 16 hex characters anyway, so this only moves the error earlier.
        if (serial.Length != 16 || !serial.All(Uri.IsHexDigit))
        {
            WriteMessage($"'{serial}' is not a valid serial number - expected 16 hex characters.");
            Divide();

            return (null, false);
        }

        return (serial, true);
    }

    // The serial lives in an empty <serial>.ID file at the root of the install path, matching what
    // the Coin-Op license portal hands out. Filter the extension ourselves instead of globbing
    // "*.ID": the glob is case sensitive on Linux, so a lowercase .id would be missed.
    private string FindCoinOpIdFile()
    {
        List<string> idFiles;

        try
        {
            idFiles = Directory.EnumerateFiles(this.installPath)
                .Where(file => COINOP_ID_EXTENSION.Equals(Path.GetExtension(file),
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            WriteMessage($"Unable to scan '{this.installPath}' for a Coin-Op .ID file: {ex.Message}");
            Divide();

            return null;
        }

        if (idFiles.Count == 0)
        {
            return null;
        }

        string serial = Path.GetFileNameWithoutExtension(idFiles[0]);

        if (idFiles.Count > 1)
        {
            WriteMessage($"Multiple .ID files found in '{this.installPath}'. Using '{serial}'.");
        }

        return serial;
    }

    private void SaveCoinOpIdFile(string serial)
    {
        string fileName = serial + COINOP_ID_EXTENSION;

        try
        {
            File.Create(Path.Combine(this.installPath, fileName)).Dispose();
            WriteMessage($"Created {fileName} in '{this.installPath}'.");
        }
        catch (Exception ex)
        {
            // Not fatal - the license is already saved. The file just avoids re-entering the serial.
            WriteMessage($"Could not save {fileName}: {ex.Message}");
        }
    }
}
