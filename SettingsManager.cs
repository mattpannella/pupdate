using System;
using System.Text.Json;
using System.IO;

namespace pannella.analoguepocket;

public class SettingsManager
{
    private Settings _settings;
    private string _settingsFile;

    private const string OLD_DEFAULT = "pocket-roms";
    private const string NEW_DEFAULT = "openFPGA-Files";

    private const string SETTINGS_FILENAME = "pocket_updater_settings.json";

    public SettingsManager(string settingsPath, List<Core>? cores = null)
    {
        _settings = new Settings();
        string file = Path.Combine(settingsPath, SETTINGS_FILENAME);
        if (File.Exists(file))
        {
            string json = File.ReadAllText(file);
            _settings = JsonSerializer.Deserialize<Settings>(json);

            //hack to force people over to new default :)
            if(_settings.config.archive_name == OLD_DEFAULT) {
                _settings.config.archive_name = NEW_DEFAULT;
            }
        }

        //bandaid to fix old settings files
        if(_settings.config == null) {
            _settings.config = new Config();
        }
        _settingsFile = file;

        if(cores != null) {
            _initializeCoreSettings(cores);
        }

        SaveSettings();
    }

    //loop through every core, and add any missing ones to the settings file
    private void _initializeCoreSettings(List<Core> cores)
    {
        if(_settings.coreSettings == null) {
            _settings.coreSettings = new Dictionary<string,CoreSettings>();
        }
        foreach(Core core in cores)
        {
            if(!_settings.coreSettings.ContainsKey(core.identifier)) {
                EnableCore(core.identifier);
            }
        }
    }

    public bool SaveSettings()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsFile, JsonSerializer.Serialize(_settings, options));

        return true;
    }

    public void DisableCore(string name)
    {
        if(_settings.coreSettings.ContainsKey(name))
        {
            _settings.coreSettings[name].skip = true;
        }
        else
        {
            CoreSettings core = new CoreSettings();
            core.skip = true;
            _settings.coreSettings.Add(name, core);
        }
    }

    public void EnableCore(string name)
    {
        if (_settings.coreSettings.ContainsKey(name))
        {
            _settings.coreSettings[name].skip = false;
        }
        else
        {
            CoreSettings core = new CoreSettings();
            core.skip = false;
            _settings.coreSettings.Add(name, core);
        }
    }

    public void UpdateCore(CoreSettings core, string name)
    {
        if (_settings.coreSettings.ContainsKey(name))
        {
            _settings.coreSettings[name] = core;
        }
        else
        {
            _settings.coreSettings.Add(name, core);
        }
    }

    public CoreSettings GetCoreSettings(string name)
    {
        if (_settings.coreSettings.ContainsKey(name))
        {
            return _settings.coreSettings[name];
        }
        else
        {
            return new CoreSettings();
        }
    }

    public Firmware GetCurrentFirmware()
    {
        return _settings.firmware;
    }

    public void SetFirmwareVersion(string version)
    {
        _settings.firmware.version = version;
        SaveSettings();
    }

    public Config GetConfig()
    {
        return _settings.config;
    }

    public void UpdateConfig(Config config)
    {
        _settings.config = config;
    }

}
