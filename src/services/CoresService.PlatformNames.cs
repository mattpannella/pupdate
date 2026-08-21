using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella.Services;

public partial class CoresService
{
    internal static string PLATFORM_NAMES_END_POINT =
        "https://raw.githubusercontent.com/mattpannella/pupdate/main/platform_names.json";

    private const string PLATFORM_NAMES_FILE = "platform_names.json";

    private Dictionary<string, string> platformNameOverrides;

    public Dictionary<string, string> PlatformNameOverrides
    {
        get
        {
            if (platformNameOverrides != null)
            {
                return platformNameOverrides;
            }

            try
            {
                string json = HttpHelper.Instance.GetHTML(PLATFORM_NAMES_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    platformNameOverrides = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
            }
            catch (Exception ex)
            {
                WriteMessage($"There was a error loading the {PLATFORM_NAMES_FILE} file from GitHub.");
                WriteMessage(this.settingsService.Debug.show_stack_traces
                    ? ex.ToString()
                    : Util.GetExceptionMessage(ex));
            }

            return platformNameOverrides ??= new Dictionary<string, string>();
        }
    }

    private void ApplyPlatformNameOverrides(Dictionary<string, Platform> platformsById)
    {
        if (platformsById == null || PlatformNameOverrides.Count == 0)
        {
            return;
        }

        foreach (var (platformId, name) in PlatformNameOverrides)
        {
            if (platformsById.TryGetValue(platformId, out Platform platform)
                && platform != null
                && (string.IsNullOrWhiteSpace(platform.name) || platform.name == platformId))
            {
                platform.name = name;
            }
        }
    }
}
