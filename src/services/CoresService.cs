using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;

namespace Pannella.Services;

public static class CoresService
{
    private const string END_POINT = "https://openfpga-cores-inventory.github.io/analogue-pocket/api/v2/cores.json";

    public static async Task<List<Core>> GetCores()
    {
        string json = await HttpHelper.Instance.GetHTML(END_POINT);

        try
        {
            Dictionary<string, List<Core>> parsed = JsonSerializer.Deserialize<Dictionary<string, List<Core>>>(json);

            if (parsed.TryGetValue("data", out var cores))
            {
                return cores;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        throw new Exception("Error communicating with openFPGA Cores API");
    }
}
