using System.Text.Json;

namespace pannella.analoguepocket;

public static class CoresService
{
    private const string END_POINT = "https://joshcampbell191.github.io/openfpga-cores-inventory/api/v1/analogue-pocket/cores.json";
    private const string OTHER = "https://raw.githubusercontent.com/mattpannella/pocket_core_autoupdate_net/develop/pocket_updater_cores.json";

    public static async Task<List<Core>> GetCores()
    {
        string json = await HttpHelper.GetHTML(END_POINT);
        Dictionary<string, List<Core>> parsed = JsonSerializer.Deserialize<Dictionary<string, List<Core>>>(json);

        if(parsed.ContainsKey("data")) {
            return parsed["data"];
        } else {
            throw new Exception("Error communicating with openFPGA Cores API");
        }
    }

    public static async Task<List<Core>> GetOtherCores()
    {
        string json = await HttpHelper.GetHTML(OTHER);
        List<Core> parsed = JsonSerializer.Deserialize<List<Core>>(json);

        return parsed;
    }
}