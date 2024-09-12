using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.DisplayModes;

namespace Pannella.Services;

public partial class CoresService
{
    private const string DISPLAY_MODES_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/display_modes.json";

    private Dictionary<string, List<DisplayMode>> displayModesList;

    public Dictionary<string, List<DisplayMode>> DisplayModes
    {
        get { return displayModesList ??= GetDisplayModesList(); }
    }

    private Dictionary<string, List<DisplayMode>> GetDisplayModesList()
    {
#if DEBUG
        string json = File.ReadAllText("display_modes.json");
#else
        string json = this.settingsService.GetConfig().use_local_display_modes
            ? File.ReadAllText("display_modes.json")
            : HttpHelper.Instance.GetHTML(DISPLAY_MODES_END_POINT);
#endif

        DisplayModes localDisplayModes = JsonConvert.DeserializeObject<DisplayModes>(json);

        return localDisplayModes.display_modes;
    }

    public List<DisplayMode> GetAllDisplayModes()
    {
        List<DisplayMode> displayModes = new List<DisplayMode>();

        foreach (string key in this.DisplayModes.Keys)
        {
            displayModes.AddRange(this.DisplayModes[key]);
        }

        return displayModes;
    }

    public List<DisplayMode> GetCuratedDisplayModes(string[] platformIds)
    {
        List<DisplayMode> displayModes = new List<DisplayMode>();

        foreach (string key in this.DisplayModes.Keys.Where(key => key == "all" || platformIds.Contains(key)))
        {
            displayModes.AddRange(this.DisplayModes[key]);
        }

        return displayModes;
    }
}
