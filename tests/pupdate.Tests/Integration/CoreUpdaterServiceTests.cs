using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.Events;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Models.Settings;
using Pannella.Models.Updater;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Release = Pannella.Models.OpenFPGA_Cores_Inventory.V3.Release;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoreUpdaterServiceTests : IDisposable
{
    private const string CoreId = "test.core";
    private const string PlatformId = "test_platform";
    private const string PlatformName = "Test Platform";
    private const string Version = "1.0.0";

    private readonly WireMockFixture _mock;
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;

    public CoreUpdaterServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();

        // RetrieveKeys() may prompt on stdin if config goes wrong. Redirect to Null so any
        // accidental ReadLine returns immediately rather than deadlocking the test.
        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);

        _fx = new OrchestrationFixture();
    }

    public void Dispose()
    {
        Console.SetIn(_origStdin);
        _fx.Dispose();
    }

    private (CoreUpdaterService updater, UpdateProcessCompleteEventArgs[] captured) BuildUpdater()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        // Sanity-check the settings deserialized as expected (catches typos in fixture JSON
        // before we try to assert orchestration outcomes — see risk #6).
        ServiceHelper.SettingsService.Config.download_assets.Should().BeFalse(
            "fixture must disable asset downloads to keep this test hermetic");
        ServiceHelper.SettingsService.Config.download_firmware.Should().BeFalse();

        var updater = new CoreUpdaterService(
            _fx.PocketDir,
            ServiceHelper.CoresService.Cores,
            ServiceHelper.FirmwareService,
            ServiceHelper.SettingsService,
            ServiceHelper.CoresService);

        var captured = new UpdateProcessCompleteEventArgs[1];
        updater.UpdateProcessComplete += (_, e) => captured[0] = e;

        return (updater, captured);
    }

    private string StubCoreReleaseZipDownload(string downloadPath = "/release.zip")
    {
        string zipPath = _fx.BuildCoreReleaseZip(CoreId, PlatformId, Version, PlatformName);
        var bytes = File.ReadAllBytes(zipPath);

        _mock.Server
            .Given(Request.Create().WithPath(downloadPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(bytes));

        return _mock.BaseUrl + downloadPath;
    }

    private static Platform BuildPlatform() => new()
    {
        id = PlatformId,
        category = "Console",
        name = PlatformName,
        manufacturer = "Test",
        year = 1990
    };

    [Fact]
    public void RunUpdates_FreshInstall_PlacesCoreFiles_AndFiresEvent()
    {
        // Arrange
        string downloadUrl = StubCoreReleaseZipDownload();
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            CoreId, PlatformId, Version, downloadUrl);
        _fx.WriteInventory(new[] { inventoryCore }, new[] { BuildPlatform() });
        _fx.WriteSettings();

        var (updater, captured) = BuildUpdater();

        // Act
        updater.RunUpdates(new[] { CoreId });

        // Assert
        File.Exists(Path.Combine(_fx.PocketDir, "Cores", CoreId, "core.json"))
            .Should().BeTrue("the fixture release zip should have been extracted into the pocket dir");

        captured[0].Should().NotBeNull("UpdateProcessComplete must fire on every RunUpdates call");
        captured[0]!.InstalledCores.Should().ContainSingle(c =>
            c["core"] == CoreId && c["version"] == Version && c["platform"] == PlatformName);
        captured[0]!.MissingLicenses.Should().BeEmpty();
    }

    [Fact]
    public void RunUpdates_SkipFlag_DoesNotInstall()
    {
        // Arrange — stub the URL but expect zero hits.
        string downloadUrl = StubCoreReleaseZipDownload();
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            CoreId, PlatformId, Version, downloadUrl);
        _fx.WriteInventory(new[] { inventoryCore }, new[] { BuildPlatform() });
        _fx.WriteSettings(new Dictionary<string, CoreSettings>
        {
            [CoreId] = new CoreSettings { skip = true }
        });

        var (updater, captured) = BuildUpdater();

        // Act
        updater.RunUpdates(new[] { CoreId });

        // Assert
        Directory.Exists(Path.Combine(_fx.PocketDir, "Cores", CoreId)).Should().BeFalse();
        _mock.Server.LogEntries.Should().BeEmpty(
            "skip=true should short-circuit before any download");

        captured[0].Should().NotBeNull();
        captured[0]!.InstalledCores.Should().BeEmpty();
    }

    [Fact]
    public void RunUpdates_AlreadyUpToDate_DoesNotReinstall()
    {
        // Arrange — pre-create core.json at the same version the inventory advertises.
        string preInstalledDir = Path.Combine(_fx.PocketDir, "Cores", CoreId);
        Directory.CreateDirectory(preInstalledDir);
        File.WriteAllText(Path.Combine(preInstalledDir, "core.json"),
            $$"""
            {
              "core": {
                "magic": "APF_VER_1",
                "metadata": {
                  "platform_ids": ["{{PlatformId}}"],
                  "shortname": "{{CoreId}}",
                  "description": "Test",
                  "author": "test",
                  "url": "u",
                  "version": "{{Version}}",
                  "date_release": "2024-01-01"
                },
                "framework": { "version_required": "0", "sleep_supported": false }
              }
            }
            """);

        string downloadUrl = StubCoreReleaseZipDownload();
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            CoreId, PlatformId, Version, downloadUrl);
        _fx.WriteInventory(new[] { inventoryCore }, new[] { BuildPlatform() });
        _fx.WriteSettings();

        var (updater, captured) = BuildUpdater();

        // Act
        updater.RunUpdates(new[] { CoreId });

        // Assert
        _mock.Server.LogEntries.Should().BeEmpty(
            "local version equals remote version → orchestration must not download anything");
        captured[0]!.InstalledCores.Should().BeEmpty(
            "no install summary entry when already up to date");
    }

    [Fact]
    public void RunUpdates_PinnedVersion_UsesPinnedDownloadUrl()
    {
        // Arrange — fixture has 1.0.0 (latest, default url) AND 0.9.0 (pinned, different url).
        // Pin the core to 0.9.0; expect the 0.9.0 url to be hit and not the 1.0.0 url.
        const string pinnedVersion = "0.9.0";

        string latestUrl = StubCoreReleaseZipDownload("/release-1.0.0.zip");
        // Build an old-version zip so the pinned download produces a valid install.
        string pinnedZipPath = _fx.BuildCoreReleaseZip(CoreId, PlatformId, pinnedVersion, PlatformName);
        // Overwrite the path so the older zip is unique.
        File.Move(pinnedZipPath, pinnedZipPath + ".pinned", overwrite: true);
        var pinnedBytes = File.ReadAllBytes(pinnedZipPath + ".pinned");
        _mock.Server
            .Given(Request.Create().WithPath("/release-0.9.0.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(pinnedBytes));
        string pinnedUrl = _mock.BaseUrl + "/release-0.9.0.zip";

        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            CoreId, PlatformId, Version, latestUrl);
        // Add the older release as a second entry in the releases list.
        inventoryCore.releases.Add(new Release
        {
            download_url = pinnedUrl,
            requires_license = false,
            core = new ReleaseCore
            {
                metadata = new ReleaseMetadata
                {
                    platform_ids = new List<string> { PlatformId },
                    version = pinnedVersion,
                    date_release = "2023-01-01"
                }
            }
        });

        _fx.WriteInventory(new[] { inventoryCore }, new[] { BuildPlatform() });
        _fx.WriteSettings(new Dictionary<string, CoreSettings>
        {
            [CoreId] = new CoreSettings { pinned_version = pinnedVersion }
        });

        var (updater, captured) = BuildUpdater();

        // Act
        updater.RunUpdates(new[] { CoreId });

        // Assert
        var paths = _mock.Server.LogEntries
            .Select(e => e.RequestMessage.AbsolutePath)
            .ToList();
        paths.Should().Contain("/release-0.9.0.zip", "pinned version's URL must be downloaded");
        paths.Should().NotContain("/release-1.0.0.zip", "the latest URL must be skipped when pinned");

        captured[0]!.InstalledCores.Should().ContainSingle(c =>
            c["core"] == CoreId && c["version"] == pinnedVersion);
    }

    [Fact]
    public void RunUpdates_RequiresLicense_NoKey_RecordsMissingLicense()
    {
        // Arrange — inventory core with requires_license=true and updaters.license set;
        // no license file pre-placed in {pocket}/Licenses/.
        string downloadUrl = StubCoreReleaseZipDownload();
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            CoreId, PlatformId, Version, downloadUrl, requiresLicense: true);
        inventoryCore.releases[0].updaters = new Updaters
        {
            license = new License { filename = "test.key" }
        };

        _fx.WriteInventory(new[] { inventoryCore }, new[] { BuildPlatform() });
        _fx.WriteSettings();

        var (updater, captured) = BuildUpdater();

        // Act
        updater.RunUpdates(new[] { CoreId });

        // Assert — license check fails → core is skipped, MissingLicenses populated.
        Directory.Exists(Path.Combine(_fx.PocketDir, "Cores", CoreId)).Should().BeFalse(
            "missing license must short-circuit before download");

        captured[0].Should().NotBeNull();
        captured[0]!.MissingLicenses.Should().Contain(CoreId);
        captured[0]!.InstalledCores.Should().BeEmpty();

        _mock.Server.LogEntries.Should().BeEmpty(
            "missing license must short-circuit before any HTTP call");
    }
}
