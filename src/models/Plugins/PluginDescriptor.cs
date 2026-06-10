namespace Pannella.Models.Plugins;

public class PluginDescriptor
{
    public string WasmPath { get; set; }
    public string DirectoryName { get; set; }
    public string PluginDirectory { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string LogoUrl { get; set; }
    public List<string> AllowedHosts { get; set; } = new();
    public string Repo { get; set; }
    public string InstalledTag { get; set; }
}
