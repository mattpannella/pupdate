using Newtonsoft.Json;
using Pannella.Models.Plugins;

namespace Pannella.Services;

public partial class CoresService
{
    private const string PLUGIN_CATALOG_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/plugins.json";
    private const string PLUGIN_CATALOG_FILE = "plugins.json";

    private List<PluginCatalogEntry> pluginCatalogList;

    public List<PluginCatalogEntry> PluginCatalogList
    {
        get
        {
            if (pluginCatalogList == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.Config.use_local_plugins_catalog,
                    PLUGIN_CATALOG_FILE,
                    PLUGIN_CATALOG_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var catalog = JsonConvert.DeserializeObject<PluginCatalog>(json);

                        pluginCatalogList = catalog?.plugins?
                            .Where(entry => entry.Slug != null)
                            .ToList();
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing {PLUGIN_CATALOG_FILE}: {ex.Message}");
                    }
                }

                pluginCatalogList ??= new List<PluginCatalogEntry>();
            }

            return pluginCatalogList;
        }
    }

    public PluginCatalogEntry GetPluginCatalogEntry(string idOrRepo)
    {
        if (string.IsNullOrWhiteSpace(idOrRepo))
            return null;

        string slug = PluginService.NormalizeRepoSlug(idOrRepo);

        return this.PluginCatalogList.FirstOrDefault(entry =>
            string.Equals(entry.id, idOrRepo, StringComparison.OrdinalIgnoreCase) ||
            (slug != null && string.Equals(entry.Slug, slug, StringComparison.OrdinalIgnoreCase)));
    }
}
