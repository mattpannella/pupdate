using Newtonsoft.Json;
using Pannella.Models.DisplayModes;

namespace Pannella.Services;

public partial class CoresService
{
    private const string DISPLAY_MODES_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/display_modes.json";
    private const string DISPLAY_MODES_FILE = "display_modes.json";

    private List<DisplayMode> allDisplayModesList;
    private Dictionary<string, List<DisplayMode>> displayModesList;

    public List<DisplayMode> AllDisplayModes
    {
        get
        {
            if (this.allDisplayModesList == null)
            {
                this.allDisplayModesList = new List<DisplayMode>();

                foreach (string key in this.DisplayModes.Keys)
                {
                    this.allDisplayModesList.AddRange(this.DisplayModes[key]);
                }
            }

            return this.allDisplayModesList;
        }
    }

    public Dictionary<string, List<DisplayMode>> DisplayModes
    {
        get
        {
            if (this.displayModesList == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.Config.use_local_display_modes,
                    DISPLAY_MODES_FILE,
                    DISPLAY_MODES_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var localDisplayModes = JsonConvert.DeserializeObject<DisplayModes>(json);

                        displayModesList = localDisplayModes.display_modes;
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
                    this.displayModesList = new Dictionary<string, List<DisplayMode>>();
                }
            }

            return this.displayModesList;
        }
    }
}
