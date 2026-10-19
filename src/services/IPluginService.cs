using Pannella.Models.Plugins;

namespace Pannella.Services;

public interface IPluginService
{
    List<PluginDescriptor> Discover();
    bool InstallFromGithub(string repoSpec);
    bool Uninstall(PluginDescriptor descriptor);
    string CheckForUpdate(PluginDescriptor descriptor);
    bool Update(PluginDescriptor descriptor);
    PluginRunResult Run(PluginDescriptor descriptor, bool unattended = false);
    PluginDescriptor FindByRepo(string repoSpec);
    PluginDescriptor FindByName(string nameOrDirectory);
}
