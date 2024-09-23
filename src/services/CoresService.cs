using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella.Services;

public partial class CoresService : BaseProcess
{
    private const string CORES_END_POINT = "https://openfpga-cores-inventory.github.io/analogue-pocket/api/v2/cores.json";
    private const string CORES_FILE = "cores.json";
    private const string ZIP_FILE_NAME = "core.zip";

    private readonly string installPath;
    private readonly SettingsService settingsService;
    private readonly ArchiveService archiveService;
    private readonly AssetsService assetsService;
    private static List<Core> cores;

    public List<Core> Cores
    {
        get
        {
            if (cores == null)
            {
                string json = null;

                if (this.settingsService.GetConfig().use_local_cores_inventory)
                {
                    if (File.Exists(CORES_FILE))
                    {
                        json = File.ReadAllText(CORES_FILE);
                    }
                    else
                    {
                        WriteMessage($"Local file not found: {CORES_FILE}");
                    }
                }
                else
                {
                    try
                    {
                        json = HttpHelper.Instance.GetHTML(CORES_END_POINT);
                    }
                    catch (HttpRequestException ex)
                    {
                        WriteMessage($"There was a error downloading the {CORES_FILE} file from GitHub.");
                        WriteMessage(ex.Message);
                    }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, List<Core>>>(json);

                        if (parsed.TryGetValue("data", out var coresList))
                        {
                            cores = coresList;
                            cores.AddRange(this.GetLocalCores());
                            cores = cores.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {CORES_FILE} file from the openFPGA cores inventory.");
#if DEBUG
                        WriteMessage(ex.ToString());
#else
                        WriteMessage(ex.Message);
#endif
                        throw;
                    }
                }
                else
                {
                    throw new NullReferenceException("There was an error parsing the openFPGA cores inventory.");
                }
            }

            return cores;
        }
    }

    private static List<Core> installedCores;

    public List<Core> InstalledCores
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

    public List<Core> InstalledCoresWithSponsors
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

    private static List<Core> coresNotInstalled;

    public List<Core> CoresNotInstalled
    {
        get
        {
            if (coresNotInstalled == null)
            {
                RefreshInstalledCores();
            }

            return coresNotInstalled;
        }
    }

    public CoresService(string path, SettingsService settingsService, ArchiveService archiveService,
        AssetsService assetsService)
    {
        this.installPath = path;
        this.settingsService = settingsService;
        this.archiveService = archiveService;
        this.assetsService = assetsService;
    }

    public Core GetCore(string identifier)
    {
        return this.Cores.Find(i => i.identifier == identifier);
    }

    public bool IsInstalled(string identifier)
    {
        // Should this just check the Installed Cores collection instead?
        string localCoreFile = Path.Combine(this.installPath, "Cores", identifier, "core.json");

        return File.Exists(localCoreFile);
    }

    public Core GetInstalledCore(string identifier)
    {
        return this.InstalledCores.Find(i => i.identifier == identifier);
    }

    public void RefreshInstalledCores()
    {
        installedCores = new List<Core>();
        coresNotInstalled = new List<Core>();
        installedCoresWithSponsors = new List<Core>();

        foreach (var core in cores)
        {
            if (this.IsInstalled(core.identifier))
            {
                installedCores.Add(core);

                if (core.sponsor != null)
                {
                    installedCoresWithSponsors.Add(core);
                }
            }
            else
            {
                coresNotInstalled.Add(core);
            }
        }

        installedCores = installedCores.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
        coresNotInstalled = coresNotInstalled.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
        installedCoresWithSponsors = installedCoresWithSponsors.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
    }

    public bool Install(Core core, bool clean = false)
    {
        if (core.repository == null)
        {
            WriteMessage("Core installed manually. Skipping.");

            return false;
        }

        if (clean && this.IsInstalled(core.identifier))
        {
            this.Delete(core.identifier, core.platform_id);
        }

        // iterate through assets to find the zip release
        if (this.InstallGithubAsset(core.identifier, core.platform_id, core.download_url))
        {
            this.ReplaceCheck(core.identifier);

            // not resetting the pocket extras on a clean install (a.k.a reinstall)
            // the combination cores and variant cores aren't affected
            // the additional assets extras just add roms so they're not affected either
            this.CheckForPocketExtras(core.identifier);

            // reset the display modes customizations on a clean install (a.k.a reinstall)
            if (clean)
            {
                this.settingsService.DisableDisplayModes(core.identifier);
                this.settingsService.Save();
            }
            else
            {
                this.CheckForDisplayModes(core.identifier);
            }

            return true;
        }

        return false;
    }

    public void Uninstall(string identifier, string platformId, bool nuke = false)
    {
        WriteMessage($"Uninstalling {identifier}...");

        this.Delete(identifier, platformId, nuke);

        this.settingsService.DisableCore(identifier);
        this.settingsService.DisablePocketExtras(identifier);
        this.settingsService.DisableDisplayModes(identifier);
        this.settingsService.Save();
        this.RefreshInstalledCores();

        WriteMessage("Finished.");
        Divide();
    }

    public void Delete(string identifier, string platformId, bool nuke = false)
    {
        List<string> folders = new List<string> { "Cores", "Presets", "Settings" };

        foreach (string folder in folders)
        {
            string path = Path.Combine(this.installPath, folder, identifier);

            if (Directory.Exists(path))
            {
                WriteMessage($"Deleting {path}...");
                Directory.Delete(path, true);
            }
        }

        if (nuke)
        {
            string path = Path.Combine(this.installPath, "Assets", platformId, identifier);

            if (Directory.Exists(path))
            {
                WriteMessage($"Deleting {path}...");
                Directory.Delete(path, true);
            }
        }
    }

    private string GetServerJsonFile(bool useLocalFile, string fileName, string uri)
    {
        string json = null;
#if !DEBUG
        if (useLocalFile)
        {
#endif
            if (File.Exists(fileName))
            {
                json = File.ReadAllText(fileName);
            }
            else
            {
                WriteMessage($"Local file not found: {fileName}");
            }
#if !DEBUG
        }
        else
        {
            try
            {
                json = HttpHelper.Instance.GetHTML(uri);
            }
            catch (HttpRequestException ex)
            {
                WriteMessage($"There was a error downloading the {fileName} file from GitHub.");
                WriteMessage(ex.Message);
            }
        }
#endif
        return json;
    }
}
