using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Models.Settings;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public class SettingsService
{
    private const string OLD_DEFAULT = "pocket-roms";
    private const string NEW_DEFAULT = "openFPGA-Files";

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
            settings = JsonSerializer.Deserialize<Settings>(json);

            // hack to force people over to new default :)
            if (settings.config.archive_name == OLD_DEFAULT)
            {
                settings.config.archive_name = NEW_DEFAULT;
            }
        }

        // bandaid to fix old settings files
        settings.config ??= new Config();
        this.settingsFile = file;

        if (cores != null)
        {
            this.InitializeCoreSettings(cores);
        }

        Save();
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(this.settingsFile, JsonSerializer.Serialize(settings, options));
    }

    /// <summary>
    /// loop through every core, and add any missing ones to the settings file
    /// </summary>
    public void InitializeCoreSettings(List<Core> cores)
    {
        settings.coreSettings ??= new Dictionary<string, CoreSettings>();

        foreach (Core core in cores)
        {
            if (!settings.coreSettings.ContainsKey(core.identifier))
            {
                this.missingCores.Add(core);
            }
        }
    }

    public void EnableCore(string name, bool? pocketExtras = null, string pocketExtrasVersion = null)
    {
        if (!settings.coreSettings.TryGetValue(name, out CoreSettings coreSettings))
        {
            coreSettings = new CoreSettings();

            settings.coreSettings.Add(name, coreSettings);
        }

        coreSettings.skip = false;

        if (pocketExtras.HasValue)
            coreSettings.pocket_extras = pocketExtras.Value;

        if (!string.IsNullOrEmpty(pocketExtrasVersion))
            coreSettings.pocket_extras_version = pocketExtrasVersion;
    }

    public void DisableCore(string name)
    {
        if (settings.coreSettings.TryGetValue(name, out CoreSettings value))
        {
            value.skip = true;
        }
        else
        {
            CoreSettings core = new CoreSettings { skip = true };

            settings.coreSettings.Add(name, core);
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
        return settings.coreSettings.TryGetValue(name, out CoreSettings value)
            ? value
            : new CoreSettings();
    }
}
