using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public class CoresService : Base
{
    private const string END_POINT = "https://openfpga-cores-inventory.github.io/analogue-pocket/api/v2/cores.json";

    public static List<Core> GetOpenFpgaCoresInventory()
    {
        string json = HttpHelper.Instance.GetHTML(END_POINT);
        Dictionary<string, List<Core>> parsed = JsonSerializer.Deserialize<Dictionary<string, List<Core>>>(json);

        if (parsed.TryGetValue("data", out var cores))
        {
            return cores;
        }

        throw new Exception("Error communicating with openFPGA Cores API");
    }
}
