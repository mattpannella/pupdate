// ReSharper disable InconsistentNaming

using Newtonsoft.Json;
using Pannella.Models.Updater;

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public class Core
{
    public string id { get; set; }
    public Repository repository { get; set; }
    public List<Release> releases { get; set; }

    public Release release { get; set; }

    public string platform_id { get; set; }

    public Platform platform { get; set; }

    public string download_url { get; set; }
    public string version { get; set; }
    public string release_date { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool requires_license { get; set; }

    public Updaters updaters { get; set; }

    public string license_slot_id;

    public int license_slot_platform_id_index;

    public string license_slot_filename;

    public override string ToString()
    {
        return platform?.name != null ? $"{platform.name} ({id})" : id ?? string.Empty;
    }
}
