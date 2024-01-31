using System.Text.Json;

namespace pannella.analoguepocket;

public static class CoresService
{
    private const string END_POINT = "https://openfpga-cores-inventory.github.io/analogue-pocket/api/v2/cores.json";

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
}