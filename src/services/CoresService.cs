using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella.Services;

public partial class CoresService : BaseProcess
{
    private const string CORES_END_POINT = "https://openfpga-library.github.io/analogue-pocket/api/v3/cores.json";
    private const string PLATFORMS_END_POINT = "https://openfpga-library.github.io/analogue-pocket/api/v3/platforms.json";
    private const string CORES_FILE = "cores.json";
    private const string PLATFORMS_FILE = "platforms.json";

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
                Dictionary<string, Platform> platformsById = null;

                if (this.settingsService.Config.use_local_cores_inventory)
                {
                    if (File.Exists(CORES_FILE) && File.Exists(PLATFORMS_FILE))
                    {
                        json = File.ReadAllText(CORES_FILE);
                        string platformsJson = File.ReadAllText(PLATFORMS_FILE);

                        if (string.IsNullOrEmpty(platformsJson))
                            throw new InvalidOperationException($"Local {PLATFORMS_FILE} is empty. Both {CORES_FILE} and {PLATFORMS_FILE} (v3 format) are required when using local cores inventory.");

                        var platformsWrapper = JsonConvert.DeserializeObject<PlatformsResponseWrapper>(platformsJson);

                        if (platformsWrapper?.data == null)
                            throw new InvalidOperationException($"Local {PLATFORMS_FILE} could not be parsed or has no data. Both files must be v3 format from the openFPGA Library API.");

                        platformsById = platformsWrapper.data
                            .Where(p => !string.IsNullOrEmpty(p?.id))
                            .ToDictionary(p => p.id, p => p);
                    }
                    else
                    {
                        if (!File.Exists(CORES_FILE))
                            WriteMessage($"Local file not found: {CORES_FILE}");

                        if (!File.Exists(PLATFORMS_FILE))
                            WriteMessage($"Local file not found: {PLATFORMS_FILE}. When using local cores inventory, both {CORES_FILE} and {PLATFORMS_FILE} (v3 format) are required.");
                    }
                }
                else
                {
                    try
                    {
                        string platformsJson = HttpHelper.Instance.GetHTML(PLATFORMS_END_POINT);

                        if (string.IsNullOrEmpty(platformsJson))
                            throw new InvalidOperationException("Failed to download platforms catalog from the openFPGA Library API (platforms.json).");

                        var platformsWrapper = JsonConvert.DeserializeObject<PlatformsResponseWrapper>(platformsJson);

                        if (platformsWrapper?.data == null)
                            throw new InvalidOperationException("Platforms catalog (platforms.json) could not be parsed or has no data.");

                        platformsById = platformsWrapper.data
                            .Where(p => !string.IsNullOrEmpty(p?.id))
                            .ToDictionary(p => p.id, p => p);

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
                        var coresWrapper = JsonConvert.DeserializeObject<CoresResponseWrapper>(json);

                        if (coresWrapper?.data == null)
                            throw new InvalidOperationException($"The {CORES_FILE} file from the openFPGA cores inventory could not be parsed or has no data.");

                        List<Core> coresList = new List<Core>();

                        foreach (Core inventoryCore in coresWrapper.data)
                        {
                            if (TrySetReleaseAndPlatform(inventoryCore, platformsById))
                                coresList.Add(inventoryCore);
                        }

                        if (settingsService.Config.no_analogizer_variants)
                            CORES = coresList.Where(core => !IsAnalogizerVariant(core.id)).ToList();
                        else
                            CORES = coresList;

                        CORES.AddRange(this.GetLocalCores());
                        CORES = CORES.OrderBy(c => c.id.ToLowerInvariant()).ToList();
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
                    throw new NullReferenceException(this.settingsService.Config.use_local_cores_inventory
                        ? $"Local cores inventory requires both {CORES_FILE} and {PLATFORMS_FILE} (v3 format) in the current directory."
                        : "There was an error parsing the openFPGA cores inventory.");
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
        return this.Cores.Find(i => i.id == identifier);
    }

    public bool IsInstalled(string identifier)
    {
        // Should this just check the Installed Cores collection instead?
        string localCoreFile = Path.Combine(this.installPath, "Cores", identifier, "core.json");

        return File.Exists(localCoreFile);
    }

    public Core GetInstalledCore(string identifier)
    {
        return this.InstalledCores.Find(i => i.id == identifier);
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
            if (this.IsInstalled(core.id))
            {
                INSTALLED_CORES.Add(core);

                if (core.repository?.funding != null)
                {
                    var info = ServiceHelper.CoresService.ReadCoreJson(core.id);
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

                if (this.settingsService.GetCoreSettings(core.id).display_modes)
                {
                    INSTALLED_CORES_WITH_CUSTOM_DISPLAY_MODES.Add(core);
                }
            }
            else
            {
                CORES_NOT_INSTALLED.Add(core);
            }
        }

        INSTALLED_CORES = INSTALLED_CORES.OrderBy(c => c.id.ToLowerInvariant()).ToList();
        CORES_NOT_INSTALLED = CORES_NOT_INSTALLED.OrderBy(c => c.id.ToLowerInvariant()).ToList();
    }

    public bool Install(Core core, bool clean = false)
    {
        if (core.repository == null)
        {
            WriteMessage("Core installed manually. Skipping.");

            return false;
        }

        this.ClearUpdatersFile(core.id);

        if (clean && this.IsInstalled(core.id))
        {
            this.Delete(core.id, core.platform_id);
        }

        // iterate through assets to find the zip release
        if (this.InstallGithubAsset(core.id, core.platform_id, core.download_url))
        {
            this.ReplaceCheck(core.id);

            // not resetting the pocket extras on a clean install (a.k.a reinstall)
            // the combination cores and variant cores aren't affected
            // the additional assets extras just add roms so they're not affected either
            this.CheckForPocketExtras(core.id);

            // reset the display modes customizations on a clean install (a.k.a reinstall)
            if (clean)
            {
                this.settingsService.DisableDisplayModes(core.id);
                this.settingsService.Save();
            }
            else
            {
                this.CheckForDisplayModes(core.id);
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

    private static Release GetLatestRelease(List<Release> releases)
    {
        if (releases == null || releases.Count == 0)
            return null;

        if (releases.Count == 1)
            return releases[0];

        Release latest = releases[0];
        string latestDate = latest.core?.metadata?.date_release;

        for (int i = 1; i < releases.Count; i++)
        {
            string d = releases[i].core?.metadata?.date_release;
            if (string.IsNullOrEmpty(d))
                continue;

            if (string.IsNullOrEmpty(latestDate) || string.CompareOrdinal(d, latestDate) > 0)
            {
                latest = releases[i];
                latestDate = d;
            }
        }

        return latest;
    }

    private static bool TrySetReleaseAndPlatform(
        Core core,
        Dictionary<string, Platform> platformsById)
    {
        if (core?.releases == null || core.releases.Count == 0)
            return false;

        Release release = GetLatestRelease(core.releases);
        if (release?.core?.metadata == null)
            return false;

        var meta = release.core.metadata;
        string platformId = meta.platform_ids is { Count: > 0 }
            ? meta.platform_ids[0]
            : null;

        if (string.IsNullOrEmpty(platformId))
            return false;

        if (platformsById == null)
            throw new InvalidOperationException(
                "Platforms catalog was not loaded. Cannot set platform without platforms.json.");

        if (!platformsById.TryGetValue(platformId, out Platform platform))
            throw new InvalidOperationException(
                $"Core '{core.id}' references platform_id '{platformId}' which is not in the platforms catalog. " +
                "Ensure platforms.json is loaded and contains this platform.");

        core.release = release;
        core.platform_id = platformId;
        core.platform = platform;
        core.download_url = release.download_url;
        core.version = meta.version;
        core.release_date = meta.date_release;
        core.requires_license = release.requires_license;
        core.updaters = release.updaters;

        return true;
    }
}
