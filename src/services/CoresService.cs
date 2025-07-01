using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.OpenFPGA_Cores_Inventory;

namespace Pannella.Services;

public partial class CoresService : BaseProcess
{
    private const string CORES_END_POINT = "https://openfpga-library.github.io/analogue-pocket/api/v2/cores.json";
    private const string CORES_FILE = "cores.json";

    private const string UPDATERS_FILE = "updaters.json";
    private const string ZIP_FILE_NAME = "core.zip";

    private readonly string installPath;
    private readonly SettingsService settingsService;
    private readonly ArchiveService archiveService;
    private readonly AssetsService assetsService;
    private static List<Core> CORES;

    public List<Core> Cores
    {
        get
        {
            if (CORES == null)
            {
                string json = null;

                if (this.settingsService.Config.use_local_cores_inventory)
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
                            if (settingsService.Config.no_analogizer_variants)
                            {
                                //filter the list if the setting is on
                                CORES = coresList.Where(core => !IsAnalogizerVariant(core.identifier)).ToList();
                            }
                            else 
                            {
                                CORES = coresList;
                            }
                            CORES.AddRange(this.GetLocalCores());
                            CORES = CORES.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteMessage($"There was an error parsing the {CORES_FILE} file from the openFPGA cores inventory.");
                        WriteMessage(this.settingsService.Debug.show_stack_traces
                            ? ex.ToString()
                            : Util.GetExceptionMessage(ex));

                        throw;
                    }
                }
                else
                {
                    throw new NullReferenceException("There was an error parsing the openFPGA cores inventory.");
                }
            }

            return CORES;
        }
    }

    private static List<Core> INSTALLED_CORES;

    public List<Core> InstalledCores
    {
        get
        {
            if (INSTALLED_CORES == null)
            {
                this.RefreshInstalledCores();
            }

            return INSTALLED_CORES;
        }
    }

    private static Dictionary<string, List<Core>> INSTALLED_CORES_WITH_SPONSORS;

    public Dictionary<string, List<Core>> InstalledCoresWithSponsors
    {
        get
        {
            if (INSTALLED_CORES_WITH_SPONSORS == null)
            {
                this.RefreshInstalledCores();
            }

            return INSTALLED_CORES_WITH_SPONSORS;
        }
    }

    private static List<Core> INSTALLED_CORES_WITH_CUSTOM_DISPLAY_MODES;

    public List<Core> InstalledCoresWithCustomDisplayModes
    {
        get
        {
            if (INSTALLED_CORES_WITH_CUSTOM_DISPLAY_MODES == null)
            {
                this.RefreshInstalledCores();
            }

            return INSTALLED_CORES_WITH_CUSTOM_DISPLAY_MODES;
        }
    }

    private static List<Core> CORES_NOT_INSTALLED;

    public List<Core> CoresNotInstalled
    {
        get
        {
            if (CORES_NOT_INSTALLED == null)
            {
                RefreshInstalledCores();
            }

            return CORES_NOT_INSTALLED;
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

    public bool IsAnalogizerVariant(string identifier)
    {
        //eventually this should be replaced with a check for a flag
        //inside updaters.json
        return identifier.Contains("Analogizer");
    }

    public void RefreshInstalledCores()
    {
        INSTALLED_CORES = new List<Core>();
        CORES_NOT_INSTALLED = new List<Core>();
        INSTALLED_CORES_WITH_SPONSORS = new Dictionary<string, List<Core>>();
        INSTALLED_CORES_WITH_CUSTOM_DISPLAY_MODES = new List<Core>();

        foreach (var core in CORES)
        {
            if (this.IsInstalled(core.identifier))
            {
                INSTALLED_CORES.Add(core);

                if (core.sponsor != null)
                {
                    var info = ServiceHelper.CoresService.ReadCoreJson(core.identifier);
                    var author = info.metadata.author;

                    if (INSTALLED_CORES_WITH_SPONSORS.TryGetValue(author, out List<Core> authorCores))
                    {
                        authorCores.Add(core);
                    }
                    else
                    {
                        INSTALLED_CORES_WITH_SPONSORS.Add(author, new List<Core> { core });
                    }
                }

                if (this.settingsService.GetCoreSettings(core.identifier).display_modes)
                {
                    INSTALLED_CORES_WITH_CUSTOM_DISPLAY_MODES.Add(core);
                }
            }
            else
            {
                CORES_NOT_INSTALLED.Add(core);
            }
        }

        INSTALLED_CORES = INSTALLED_CORES.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
        CORES_NOT_INSTALLED = CORES_NOT_INSTALLED.OrderBy(c => c.identifier.ToLowerInvariant()).ToList();
    }

    public bool Install(Core core, bool clean = false)
    {
        if (core.repository == null)
        {
            WriteMessage("Core installed manually. Skipping.");

            return false;
        }

        this.ClearUpdatersFile(core.identifier);

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

    public void ClearUpdatersFile(string identifier)
    {
        string file = Path.Combine(this.installPath, "Cores", identifier, UPDATERS_FILE);

        if (this.IsInstalled(identifier) && File.Exists(file))
        {
            File.Delete(file);
        }
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
