using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Models.Settings;

namespace Pannella.Services;

public class SettingsService
{
    private const string OLD_SETTINGS_FILENAME = "pocket_updater_settings.json";
    private const string SETTINGS_FILENAME = "pupdate_settings.json";

    private readonly Settings settings;
    private readonly string settingsFile;
    private readonly List<Core> missingCores;

    public SettingsService(string settingsPath, List<Core> cores = null)
    {
        this.settings = new Settings();
        this.missingCores = new List<Core>();

        string file = Path.Combine(settingsPath, SETTINGS_FILENAME);
        string oldFile = Path.Combine(settingsPath, OLD_SETTINGS_FILENAME);
        string json = null;

        if (File.Exists(file))
        {
            json = File.ReadAllText(file);
        }
        else if (File.Exists(oldFile))
        {
            json = File.ReadAllText(oldFile);
            File.Delete(oldFile);
        }

        if (!string.IsNullOrEmpty(json))
        {
            settings = JsonConvert.DeserializeObject<Settings>(json);
            settings.config.Migrate();
        }

        // bandaid to fix old settings files
        settings.config ??= new Config();
        this.settingsFile = file;

        if (cores != null)
        {
            this.InitializeCoreSettings(cores);
        }

        this.Save();
    }

    public void Save()
    {
        var options = new JsonSerializerSettings { ContractResolver = ArchiveContractResolver.Instance };
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented, options);

        File.WriteAllText(this.settingsFile, json);
    }

    /// <summary>
    /// loop through every core, and add any missing ones to the settings file
    /// </summary>
    public void InitializeCoreSettings(List<Core> cores)
    {
        settings.core_settings ??= new Dictionary<string, CoreSettings>();

        foreach (Core core in cores)
        {
            // if (!settings.core_settings.ContainsKey(core.identifier))
            if (!settings.core_settings.TryGetValue(core.identifier, out var coreSettings))
            {
                this.missingCores.Add(core);
            }
            else if (coreSettings.requires_license && !core.requires_license)
            {
                this.missingCores.Add(core);
                coreSettings.requires_license = false;
            }
            else if (core.requires_license)
            {
                coreSettings.requires_license = true;
            }
        }
    }

    public void EnableCore(string name, bool? pocketExtras = null, string pocketExtrasVersion = null)
    {
        if (!settings.core_settings.TryGetValue(name, out CoreSettings coreSettings))
        {
            coreSettings = new CoreSettings();

            settings.core_settings.Add(name, coreSettings);
        }

        coreSettings.skip = false;

        if (pocketExtras.HasValue)
            coreSettings.pocket_extras = pocketExtras.Value;

        if (!string.IsNullOrEmpty(pocketExtrasVersion))
            coreSettings.pocket_extras_version = pocketExtrasVersion;
    }

    public void DisableCore(string name)
    {
        if (settings.core_settings.TryGetValue(name, out CoreSettings value))
        {
            value.skip = true;
        }
        else
        {
            CoreSettings core = new CoreSettings { skip = true };

            settings.core_settings.Add(name, core);
        }
    }

    public void DisablePocketExtras(string name)
    {
        if (settings.core_settings.TryGetValue(name, out CoreSettings value))
        {
            value.pocket_extras = false;
            value.pocket_extras_version = null;
        }
    }

    public void DisableDisplayModes(string name)
    {
        if (settings.core_settings.TryGetValue(name, out CoreSettings value))
        {
            value.display_modes = false;
            value.original_display_modes = null;
            value.selected_display_modes = null;
        }
    }

    public List<Core> GetMissingCores() => this.missingCores;

    public void EnableMissingCores()
    {
        foreach (var core in this.missingCores)
        {
            EnableCore(core.identifier);
        }
    }

    public void DisableMissingCores()
    {
        foreach (var core in this.missingCores)
        {
            DisableCore(core.identifier);
        }
    }

    public Config GetConfig()
    {
        return settings.config;
    }

    // This is used by the RetroDriven Pocket Updater Windows Application
    // ReSharper disable once UnusedMember.Global
    public void UpdateConfig(Config config)
    {
        settings.config = config;
    }

    public CoreSettings GetCoreSettings(string name)
    {
        return settings.core_settings.TryGetValue(name, out CoreSettings value)
            ? value
            : new CoreSettings();
    }
}
