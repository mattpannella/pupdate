using Newtonsoft.Json;
using Pannella.Models.DisplayModes;

namespace Pannella.Services;

public partial class CoresService
{
    private const string DISPLAY_MODES_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/display_modes.json";
    private const string DISPLAY_MODES_FILE = "display_modes.json";

    private Dictionary<string, List<DisplayMode>> displayModesList;

    private Dictionary<string, List<DisplayMode>> DisplayModes
    {
        get
        {
            if (displayModesList == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.GetConfig().use_local_display_modes,
                    DISPLAY_MODES_FILE,
                    DISPLAY_MODES_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var localDisplayModes = JsonConvert.DeserializeObject<DisplayModes>(json);

                        return localDisplayModes.display_modes;
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {DISPLAY_MODES_FILE} file.");
#if DEBUG
                        WriteMessage(ex.ToString());
#else
                        WriteMessage(ex.Message);
#endif
                    }
                }
                else
                {
                    displayModesList = new Dictionary<string, List<DisplayMode>>();
                }
            }

            return displayModesList;
        }
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
}
