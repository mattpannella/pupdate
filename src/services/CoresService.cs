using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

    private string installPath;
    private static List<Core> cores;

    public List<Core> Cores
    {
        get
        {
            if (cores == null)
            {
                string json = HttpHelper.Instance.GetHTML(END_POINT);
                Dictionary<string, List<Core>> parsed = JsonSerializer.Deserialize<Dictionary<string, List<Core>>>(json);

                if (parsed.TryGetValue("data", out var coresList))
                {
                    cores = coresList;
                    cores.AddRange(this.GetLocalCores());
                }
            }

            return cores;
        }
    }

    private static List<Core> installedCores;

    public static List<Core> InstalledCores
    {
        get
        {
            if (installedCores == null)
            {
                RefreshInstalledCores();
            }

            return installedCores;
        }
    }

    private static List<Core> installedCoresWithSponsors;

    public static List<Core> InstalledCoresWithSponsors
    {
        get
        {
            if (installedCoresWithSponsors == null)
            {
                RefreshInstalledCores();
            }

            return installedCoresWithSponsors;
        }
    }

    public CoresService(string path)
    {
        this.installPath = path;
    }

    public static Core GetCore(string identifier)
    {
        return cores.Find(i => i.identifier == identifier);
    }

    public static Core GetInstalledCore(string identifier)
    {
        return InstalledCores.Find(i => i.identifier == identifier);
    }

    public static void RefreshInstalledCores()
    {
        installedCores = cores.Where(c => c.IsInstalled()).ToList();
        installedCoresWithSponsors = installedCores.Where(c => c.sponsor != null).ToList();
    }

    private IEnumerable<Core> GetLocalCores()
    {
        string coresDirectory = Path.Combine(this.installPath, "Cores");

        // Create if it doesn't exist. -- Should we do this?
        // Stops error from being thrown if we do.
        Directory.CreateDirectory(coresDirectory);

        string[] directories = Directory.GetDirectories(coresDirectory, "*", SearchOption.TopDirectoryOnly);
        List<Core> all = new List<Core>();

        foreach (string name in directories)
        {
            string n = Path.GetFileName(name);
            var matches = Cores.Where(i => i.identifier == n);

            if (!matches.Any())
            {
                Core c = new Core { identifier = n };
                c.platform = c.ReadPlatformFile();
                all.Add(c);
            }
        }

        return all;
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
