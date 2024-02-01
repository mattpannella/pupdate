using System.Text.Json;
using Pannella.Models;
using Pannella.Models.Settings;

namespace Pannella;

public class SettingsManager
{
    private readonly Settings _settings;
    private readonly string _settingsFile;
    private readonly List<Core> _newCores = new();

    private const string OLD_DEFAULT = "pocket-roms";
    private const string NEW_DEFAULT = "openFPGA-Files";

    private const string OLD_SETTINGS_FILENAME = "pocket_updater_settings.json";
    private const string SETTINGS_FILENAME = "pupdate_settings.json";

    public SettingsManager(string settingsPath, List<Core> cores = null)
    {
        _settings = new Settings();

        string file = Path.Combine(settingsPath, SETTINGS_FILENAME);
        string oldFile = Path.Combine(settingsPath, OLD_SETTINGS_FILENAME);
        string json = null!;

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
            _settings = JsonSerializer.Deserialize<Settings>(json);

            // hack to force people over to new default :)
            if (_settings.config.archive_name == OLD_DEFAULT)
            {
                _settings.config.archive_name = NEW_DEFAULT;
            }
        }

        // bandaid to fix old settings files
        _settings.config ??= new Config();
        _settingsFile = file;

        if (cores != null)
        {
            InitializeCoreSettings(cores);
        }

        SaveSettings();
    }

    // loop through every core, and add any missing ones to the settings file
    public void InitializeCoreSettings(List<Core> cores)
    {
        _settings.coreSettings ??= new Dictionary<string, CoreSettings>();

        foreach (Core core in cores)
        {
            if (!_settings.coreSettings.ContainsKey(core.identifier))
            {
                _newCores.Add(core);
            }
        }
    }

    public List<Core> GetMissingCores() => _newCores;

    public void EnableMissingCores(List<Core> cores)
    {
        foreach (var core in cores)
        {
            EnableCore(core.identifier);
        }
    }

    public void DisableMissingCores(List<Core> cores)
    {
        foreach (var core in cores)
        {
            DisableCore(core.identifier);
        }
    }

    public void SaveSettings()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsFile, JsonSerializer.Serialize(_settings, options));
    }

    public void DisableCore(string name)
    {
        if (_settings.coreSettings.TryGetValue(name, out CoreSettings value))
        {
            value.skip = true;
        }
        else
        {
            CoreSettings core = new CoreSettings { skip = true };

            _settings.coreSettings.Add(name, core);
        }
    }

    public void EnableCore(string name)
    {
        if (_settings.coreSettings.TryGetValue(name, out CoreSettings value))
        {
            value.skip = false;
        }
        else
        {
            CoreSettings core = new CoreSettings { skip = false };

            _settings.coreSettings.Add(name, core);
        }
    }

    public CoreSettings GetCoreSettings(string name)
    {
        return _settings.coreSettings.TryGetValue(name, out CoreSettings value)
            ? value
            : new CoreSettings();
    }

    public Config GetConfig()
    {
        return _settings.config;
    }
}
