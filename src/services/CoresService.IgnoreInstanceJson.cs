using Newtonsoft.Json;
using Pannella.Models;

namespace Pannella.Services;

public partial class CoresService
{
    private const string IGNORE_INSTANCE_JSON_END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/ignore_instance.json";
    private const string IGNORE_INSTANCE_JSON_FILE = "ignore_instance.json";

    private static List<string> ignoreInstanceJson;

    private List<string> IgnoreInstanceJson
    {
        get
        {
            if (ignoreInstanceJson == null)
            {
                string json = this.GetServerJsonFile(
                    this.settingsService.GetConfig().use_local_ignore_instance_json,
                    IGNORE_INSTANCE_JSON_FILE,
                    IGNORE_INSTANCE_JSON_END_POINT);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var coreIdentifiers = JsonConvert.DeserializeObject<IgnoreInstanceJson>(json);

                        ignoreInstanceJson = coreIdentifiers.core_identifiers;
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {IGNORE_INSTANCE_JSON_FILE} file.");
#if DEBUG
                        WriteMessage(ex.ToString());
#else
                        WriteMessage(ex.Message);
#endif
                    }
                }
                else
                {
                    ignoreInstanceJson = new List<string>();
                }
            }

            return ignoreInstanceJson;
        }
    }
}
