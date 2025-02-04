using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;

namespace Pannella.Services;

public partial class CoresService
{
    private const string IGNORE_INSTANCE_JSON_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/ignore_instance.json";
    private const string IGNORE_INSTANCE_JSON_FILE = "ignore_instance.json";

    private static List<string> IGNORE_INSTANCE_JSON;

    private List<string> IgnoreInstanceJson
    {
        get
        {
            if (IGNORE_INSTANCE_JSON == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.Config.use_local_ignore_instance_json,
                    IGNORE_INSTANCE_JSON_FILE,
                    IGNORE_INSTANCE_JSON_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var coreIdentifiers = JsonConvert.DeserializeObject<IgnoreInstanceJson>(json);

                        IGNORE_INSTANCE_JSON = coreIdentifiers.core_identifiers;
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {IGNORE_INSTANCE_JSON_FILE} file.");
                        WriteMessage(this.settingsService.Debug.show_stack_traces
                            ? ex.ToString()
                            : Util.GetExceptionMessage(ex));
                    }
                }
                else
                {
                    IGNORE_INSTANCE_JSON = new List<string>();
                }
            }

            return IGNORE_INSTANCE_JSON;
        }
    }
}
