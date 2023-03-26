using System.Text.Json;

namespace pannella.analoguepocket;

public static class CoresService
{
    private const string END_POINT = "https://openfpga-cores-inventory.github.io/analogue-pocket/api/v2/cores.json";
    private const string OTHER = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/main/pocket_updater_cores.json";

    public static async Task<List<Core>> GetCores()
    {
        string json = await HttpHelper.Instance.GetHTML(END_POINT);
        Dictionary<string, List<Core>> parsed = JsonSerializer.Deserialize<Dictionary<string, List<Core>>>(json);

        if(parsed.ContainsKey("data")) {
        //    var others = await GetNonAPICores();
            var cores = parsed["data"];
          //  cores.AddRange(others);
            return cores;
            //return parsed["data"];
        } else {
            throw new Exception("Error communicating with openFPGA Cores API");
        }
    }

    public static async Task<List<Core>> GetNonAPICores()
    {
        string json = await HttpHelper.Instance.GetHTML(OTHER);
        List<Core> parsed = JsonSerializer.Deserialize<List<Core>>(json);

        return parsed;
    }
}