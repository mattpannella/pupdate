using System.Text.Json;

namespace pannella.analoguepocket;

public static class CoresService
{
    private const string END_POINT = "https://openfpga-cores-inventory.github.io/analogue-pocket/api/v2/cores.json";
    private const string OTHER = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/main/pocket_updater_cores.json";

    public static async Task<List<Core>> GetCores()
    {
        string json = await Factory.GetHttpHelper().GetHTML(END_POINT);
        Dictionary<string, List<Core>> parsed = JsonSerializer.Deserialize<Dictionary<string, List<Core>>>(json);

        if(parsed.ContainsKey("data")) {
            var cores = parsed["data"];
            return cores;
        } else {
            throw new Exception("Error communicating with openFPGA Cores API");
        }
    }

    public static async Task<List<Core>> GetNonAPICores()
    {
        string json = await Factory.GetHttpHelper().GetHTML(OTHER);
        List<Core> parsed = JsonSerializer.Deserialize<List<Core>>(json);

        return parsed;
    }
}