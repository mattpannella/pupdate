namespace Pannella.Models.Events;

public class UpdateProcessCompleteEventArgs : EventArgs
{
    public string Message { get; set; }
    public List<Dictionary<string, string>> InstalledCores { get; set; }
    public List<string> InstalledAssets { get; set; }
    public List<string> SkippedAssets { get; set; }
    public string FirmwareUpdated { get; set; } = string.Empty;
    public List<string> MissingLicenses { get; set; }
    public List<string> PluginsRun { get; set; } = new();
    public List<string> PluginsNeedInput { get; set; } = new();
    public List<string> PluginsFailed { get; set; } = new();
    public List<string> RecommendedPlugins { get; set; } = new();
    public int ErrorCount { get; set; }
    public bool SkipOutro;
}
