using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoreUpdaterServiceJotegoRenameTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;
    private readonly string _origReleases;
    private readonly string _origContents;

    public CoreUpdaterServiceJotegoRenameTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);

        // RenamedPlatformFiles uses GithubApiService.GetFiles, which hits CONTENTS.
        // The orchestration's core-zip download is stubbed at our own /release.zip path.
        _origReleases = GithubApiService.RELEASES;
        _origContents = GithubApiService.CONTENTS;
        GithubApiService.RELEASES = _mock.BaseUrl + "/repos/{0}/{1}/releases";
        GithubApiService.CONTENTS = _mock.BaseUrl + "/repos/{0}/{1}/contents/{2}";

        _fx = new OrchestrationFixture();
    }

    public void Dispose()
    {
        GithubApiService.RELEASES = _origReleases;
        GithubApiService.CONTENTS = _origContents;
        Console.SetIn(_origStdin);
        _fx.Dispose();
    }

    [Fact]
    public void RunUpdates_JotegoCore_RenamesPlatformJson_FromGithubPlatformImagesRepo()
    {
        // Arrange — jotego.PSX core. JotegoRename triggers when:
        //   - core id contains "jotego"
        //   - fix_jt_names=true (default)
        //   - GetCoreSettings(id).platform_rename=true (default)
        //   - {pocket}/Platforms/{platform_id}.json's "name" still equals the raw platform_id
        //   - RenamedPlatformFiles has an entry for that platform_id
        const string coreId = "jotego.PSX";
        const string platformId = "PSX";  // jotego id format: <author>.<platform_id>

        // (1) Stub the core release zip download.
        string releaseZipPath = _fx.BuildCoreReleaseZip(coreId, platformId, "1.0.0", "PSX");
        var releaseZipBytes = File.ReadAllBytes(releaseZipPath);
        string releaseUrl = _mock.BaseUrl + "/release.zip";
        _mock.Server
            .Given(Request.Create().WithPath("/release.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(releaseZipBytes));

        // (2) Stub the dyreschlock/pocket-platform-images contents endpoints. arcade returns
        // PSX.json with a download_url pointing at our renamed-platform stub. home returns [].
        string renamedPlatformUrl = _mock.BaseUrl + "/renamed/PSX.json";
        _mock.Server
            .Given(Request.Create()
                .WithPath("/repos/dyreschlock/pocket-platform-images/contents/arcade/Platforms")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                $$"""
                [ { "name": "PSX.json", "path": "arcade/Platforms/PSX.json", "sha":"x",
                    "size": 1, "url":"u", "html_url":"h", "download_url": "{{renamedPlatformUrl}}" } ]
                """));
        _mock.Server
            .Given(Request.Create()
                .WithPath("/repos/dyreschlock/pocket-platform-images/contents/home/Platforms")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));

        // (3) Stub the renamed-platform download (the new content that should overwrite local).
        const string renamedPlatformJson =
            """{ "platform": { "category":"Arcade", "name":"PlayStation", "manufacturer":"Sony", "year":1994 } }""";
        _mock.Server
            .Given(Request.Create().WithPath("/renamed/PSX.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(renamedPlatformJson));

        // (4) Pre-create Platforms/PSX.json with name="PSX" — the raw, unfixed name that
        // triggers the rename path. (If name were already "PlayStation", the rename would
        // be skipped.)
        Directory.CreateDirectory(Path.Combine(_fx.PocketDir, "Platforms"));
        File.WriteAllText(Path.Combine(_fx.PocketDir, "Platforms", "PSX.json"),
            """{ "platform": { "category":"Arcade", "name":"PSX", "manufacturer":"Sony", "year":1994 } }""");

        // (5) Inventory + settings.
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(coreId, platformId, "1.0.0", releaseUrl);
        var platform = new Platform
        {
            id = platformId, category = "Arcade", name = "PSX", manufacturer = "Sony", year = 1994
        };
        _fx.WriteInventory(new[] { inventoryCore }, new[] { platform });
        _fx.WriteSettings();

        // (6) Run.
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);
        var updater = new CoreUpdaterService(
            _fx.PocketDir,
            ServiceHelper.CoresService.Cores,
            ServiceHelper.FirmwareService,
            ServiceHelper.SettingsService,
            ServiceHelper.CoresService);

        updater.RunUpdates(new[] { coreId });

        // Assert — the local Platforms/PSX.json has been overwritten with the "PlayStation"
        // version pulled from dyreschlock/pocket-platform-images.
        var finalDoc = Newtonsoft.Json.Linq.JObject.Parse(
            File.ReadAllText(Path.Combine(_fx.PocketDir, "Platforms", "PSX.json")));
        finalDoc["platform"]!["name"]!.ToString().Should().Be("PlayStation",
            "JotegoRename should have downloaded the renamed platform JSON over the local file");
    }

    [Fact]
    public void RunUpdates_JotegoCore_FixJtNamesDisabled_LeavesPlatformAlone()
    {
        // Same scenario as above, but with fix_jt_names=false. JotegoRename short-circuits
        // and the local platform JSON is untouched.
        const string coreId = "jotego.PSX";
        const string platformId = "PSX";

        string releaseZipPath = _fx.BuildCoreReleaseZip(coreId, platformId, "1.0.0", "PSX");
        var releaseZipBytes = File.ReadAllBytes(releaseZipPath);
        string releaseUrl = _mock.BaseUrl + "/release.zip";
        _mock.Server
            .Given(Request.Create().WithPath("/release.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(releaseZipBytes));

        Directory.CreateDirectory(Path.Combine(_fx.PocketDir, "Platforms"));
        File.WriteAllText(Path.Combine(_fx.PocketDir, "Platforms", "PSX.json"),
            """{ "platform": { "category":"Arcade", "name":"PSX", "manufacturer":"Sony", "year":1994 } }""");

        var inventoryCore = OrchestrationFixture.BuildInventoryCore(coreId, platformId, "1.0.0", releaseUrl);
        var platform = new Platform { id = platformId, category = "Arcade", name = "PSX", manufacturer = "Sony", year = 1994 };
        _fx.WriteInventory(new[] { inventoryCore }, new[] { platform });

        // Override fix_jt_names=false in settings.
        var settings = new Pannella.Models.Settings.Settings();
        settings.config.download_firmware = false;
        settings.config.download_assets = false;
        settings.config.backup_saves = false;
        settings.config.crc_check = false;
        settings.config.fix_jt_names = false;          // <-- the key flag
        settings.config.use_local_cores_inventory = true;
        settings.config.use_local_blacklist = true;
        settings.config.use_local_pocket_extras = true;
        settings.config.use_local_display_modes = true;
        settings.config.use_local_ignore_instance_json = true;
        settings.config.use_local_pocket_library_images = true;
        File.WriteAllText(Path.Combine(_fx.SettingsDir, "pupdate_settings.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented,
                new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = ArchiveContractResolver.INSTANCE }));

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);
        var updater = new CoreUpdaterService(
            _fx.PocketDir,
            ServiceHelper.CoresService.Cores,
            ServiceHelper.FirmwareService,
            ServiceHelper.SettingsService,
            ServiceHelper.CoresService);

        updater.RunUpdates(new[] { coreId });

        // Local PSX.json untouched (still has unfixed name), and no GitHub contents call was made.
        var finalDoc = Newtonsoft.Json.Linq.JObject.Parse(
            File.ReadAllText(Path.Combine(_fx.PocketDir, "Platforms", "PSX.json")));
        finalDoc["platform"]!["name"]!.ToString().Should().Be("PSX");

        _mock.Server.LogEntries
            .Where(e => e.RequestMessage.AbsolutePath.Contains("dyreschlock"))
            .Should().BeEmpty("fix_jt_names=false must short-circuit the rename + GitHub fetch");
    }
}
