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

    public void ForceDisplayModes(List<Core> cores)
    {
        if (cores == null)
        {
            WriteMessage("List of cores is required.");
            return;
        }

        foreach (var core in cores)
        {
            this.ForceDisplayModes(core);
        }
    }

    public void ForceDisplayModes(Core core)
    {
        if (core == null)
        {
            WriteMessage("Core is required.");
            return;
        }

        core.download_assets = true;

        try
        {
            // not sure if this check is still needed
            if (core.identifier == null)
            {
                WriteMessage("Core Name is required. Skipping.");
                return;
            }

            WriteMessage("Updating " + core.identifier);
            core.AddDisplayModes();
            Divide();
        }
        catch (Exception e)
        {
            WriteMessage("Uh oh something went wrong.");
#if DEBUG
            WriteMessage(e.ToString());
#else
            WriteMessage(e.Message);
#endif
        }

        WriteMessage("Finished.");
    }
}
