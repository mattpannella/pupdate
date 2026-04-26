using System.IO.Compression;
using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Models.Settings;
using Pannella.Services;
using Release = Pannella.Models.OpenFPGA_Cores_Inventory.V3.Release;

namespace Pannella.Tests.Fixtures;

/// <summary>
/// Builds a minimal but production-shaped Pocket install layout in a temp dir for end-to-end
/// CoreUpdaterService.RunUpdates orchestration tests. Caller is responsible for invoking
/// <see cref="ServiceHelper.Initialize"/> and <see cref="Dispose"/>.
/// </summary>
public class OrchestrationFixture : IDisposable
{
    public string Root { get; }
    public string PocketDir { get; }
    public string SettingsDir { get; }
    public string WorkDir { get; }

    private readonly string _origCwd;

    public OrchestrationFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "pupdate-orch-" + Guid.NewGuid().ToString("N"));
        PocketDir = Path.Combine(Root, "pocket");
        SettingsDir = Path.Combine(Root, "settings");
        WorkDir = Path.Combine(Root, "work");

        Directory.CreateDirectory(PocketDir);
        Directory.CreateDirectory(SettingsDir);
        Directory.CreateDirectory(WorkDir);

        _origCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(WorkDir);

        // Always-empty local fallbacks for the various JSON files that CoresService loads.
        File.WriteAllText(Path.Combine(WorkDir, "blacklist.json"), "[]");
        File.WriteAllText(Path.Combine(WorkDir, "pocket_extras.json"), """{ "pocket_extras": [] }""");
        File.WriteAllText(Path.Combine(WorkDir, "display_modes.json"), """{ "display_modes": {} }""");
        File.WriteAllText(Path.Combine(WorkDir, "ignore_instance.json"), """{ "core_identifiers": [] }""");
        File.WriteAllText(Path.Combine(WorkDir, "pocket_library_images.json"),
            """{ "pocket_library_images": [] }""");
    }

    /// <summary>
    /// Writes pupdate_settings.json with safe defaults for orchestration tests:
    /// no firmware, no asset downloads, no backups, no JT/CoinOp fetches, all use_local_* on.
    /// Caller can pre-populate per-core settings via <paramref name="coreSettings"/>.
    /// </summary>
    public void WriteSettings(IDictionary<string, CoreSettings> coreSettings = null)
    {
        var settings = new Settings();
        settings.config.download_firmware = false;
        settings.config.download_assets = false;
        settings.config.backup_saves = false;
        settings.config.crc_check = false;
        settings.config.coin_op_beta = false;
        settings.config.jt_beta_github_fetch = false;
        settings.config.jt_beta_patreon_fetch = false;
        settings.config.use_local_cores_inventory = true;
        settings.config.use_local_blacklist = true;
        settings.config.use_local_pocket_extras = true;
        settings.config.use_local_display_modes = true;
        settings.config.use_local_ignore_instance_json = true;
        settings.config.use_local_pocket_library_images = true;

        if (coreSettings != null)
        {
            foreach (var kvp in coreSettings)
                settings.core_settings[kvp.Key] = kvp.Value;
        }

        var json = JsonConvert.SerializeObject(settings, Formatting.Indented,
            new JsonSerializerSettings { ContractResolver = ArchiveContractResolver.INSTANCE });
        File.WriteAllText(Path.Combine(SettingsDir, "pupdate_settings.json"), json);
    }

    /// <summary>
    /// Writes a v3-shaped cores.json + platforms.json into the working directory.
    /// </summary>
    public void WriteInventory(IEnumerable<Core> cores, IEnumerable<Platform> platforms)
    {
        var coresWrapper = new CoresResponseWrapper { data = cores.ToList() };
        var platformsWrapper = new PlatformsResponseWrapper { data = platforms.ToList() };

        File.WriteAllText(Path.Combine(WorkDir, "cores.json"),
            JsonConvert.SerializeObject(coresWrapper));
        File.WriteAllText(Path.Combine(WorkDir, "platforms.json"),
            JsonConvert.SerializeObject(platformsWrapper));
    }

    /// <summary>
    /// Builds a v3 inventory Core that <see cref="CoresService.TrySetReleaseAndPlatform"/> will accept.
    /// </summary>
    public static Core BuildInventoryCore(
        string id,
        string platformId,
        string version,
        string downloadUrl,
        bool requiresLicense = false)
    {
        return new Core
        {
            id = id,
            repository = new Repository { owner = "test-owner", name = "test-repo", platform = "github" },
            releases = new List<Release>
            {
                new Release
                {
                    download_url = downloadUrl,
                    requires_license = requiresLicense,
                    core = new ReleaseCore
                    {
                        metadata = new ReleaseMetadata
                        {
                            platform_ids = new List<string> { platformId },
                            version = version,
                            date_release = "2024-01-01"
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a zip on disk that, when extracted via ZipHelper and copied to the pocket dir,
    /// produces {pocket}/Cores/{id}/core.json and the matching Platforms/{platformId}.json.
    /// Returns the absolute path to the zip.
    /// </summary>
    public string BuildCoreReleaseZip(string id, string platformId, string version, string platformName)
    {
        string zipPath = Path.Combine(Root, $"core-release-{id}.zip");

        string coreJson =
            $$"""
            {
              "core": {
                "magic": "APF_VER_1",
                "metadata": {
                  "platform_ids": ["{{platformId}}"],
                  "shortname": "{{id}}",
                  "description": "Test core",
                  "author": "test",
                  "url": "https://example.com",
                  "version": "{{version}}",
                  "date_release": "2024-01-01"
                },
                "framework": { "version_required": "0", "sleep_supported": false }
              }
            }
            """;

        string platformJson =
            $$"""
            {
              "platform": {
                "category": "Console",
                "name": "{{platformName}}",
                "manufacturer": "Test",
                "year": 1990
              }
            }
            """;

        using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        AddEntry(zip, $"Cores/{id}/core.json", coreJson);
        AddEntry(zip, $"Platforms/{platformId}.json", platformJson);

        return zipPath;
    }

    private static void AddEntry(ZipArchive zip, string entryName, string content)
    {
        // Forward slashes only — ZIP spec requires them and ZipHelper validates path traversal.
        var entry = zip.CreateEntry(entryName);
        using var w = new StreamWriter(entry.Open());
        w.Write(content);
    }

    public void Dispose()
    {
        try { Directory.SetCurrentDirectory(_origCwd); } catch { /* dir may have been deleted */ }
        ServiceHelper.ResetForTests();
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
