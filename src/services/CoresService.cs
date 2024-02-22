using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public class CoresService : BaseProcess
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

    public void DownloadCoreAssets(List<Core> cores)
    {
        List<string> installedAssets = new List<string>();
        List<string> skippedAssets = new List<string>();
        List<string> missingBetaKeys = new List<string>();

        if (cores == null)
        {
            WriteMessage("List of cores is required.");
            return;
        }

        foreach (var core in cores)
        {
            core.download_assets = true;

            try
            {
                string name = core.identifier;

                if (name == null)
                {
                    WriteMessage("Core Name is required. Skipping.");
                    continue;
                }

                WriteMessage(core.identifier);

                var results = core.DownloadAssets();

                installedAssets.AddRange((List<string>)results["installed"]);
                skippedAssets.AddRange((List<string>)results["skipped"]);

                if ((bool)results["missingBetaKey"])
                {
                    missingBetaKeys.Add(core.identifier);
                }

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
        }

        UpdateProcessCompleteEventArgs args = new UpdateProcessCompleteEventArgs
        {
            Message = "All Done",
            InstalledAssets = installedAssets,
            SkippedAssets = skippedAssets,
            MissingBetaKeys = missingBetaKeys,
            SkipOutro = false,
        };

        OnUpdateProcessComplete(args);
    }
}
