using System.Text.Json;

namespace pannella.analoguepocket;

public static class CoresAPI
{
    private const string END_POINT = "https://joshcampbell191.github.io/openfpga-cores-inventory/api/v0/analogue-pocket/cores.json";

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
}