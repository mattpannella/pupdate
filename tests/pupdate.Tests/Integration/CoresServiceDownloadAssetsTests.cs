using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using SettingsArchive = Pannella.Models.Settings.Archive;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoresServiceDownloadAssetsTests : IDisposable
{
    private const string CoreId = "test.core";
    private const string PlatformId = "test_platform";
    private const string PlatformName = "Test Platform";
    private const string Version = "1.0.0";

    // The instance-json asset lives under a subdirectory, so BuildAssetCandidates' first
    // candidate is [platform]/[subdir]/[asset] — exactly how the reorganized openFPGA-Files
    // archive stores vectrex assets (e.g. vectrex/overlays/Pole_Position.ovr).
    private const string SlotFilename = "games/rom.bin";
    private const string DefaultArchiveName = "openFPGA-Files";
    private const string DefaultDownloadPath = "/download/openFPGA-Files/test_platform/games/rom.bin";

    private readonly WireMockFixture _mock;
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;
    private readonly string _origMetadata;
    private readonly string _origDownload;

    public CoresServiceDownloadAssetsTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();

        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);

        _origMetadata = ArchiveService.METADATA;
        _origDownload = ArchiveService.DOWNLOAD;
        ArchiveService.METADATA = _mock.BaseUrl + "/metadata/{0}";
        ArchiveService.DOWNLOAD = _mock.BaseUrl + "/download/{0}/{1}";

        _fx = new OrchestrationFixture();
    }

    public void Dispose()
    {
        Console.SetIn(_origStdin);
        ArchiveService.METADATA = _origMetadata;
        ArchiveService.DOWNLOAD = _origDownload;
        _fx.Dispose();
    }

    [Fact]
    public void DownloadAssets_CoreWithDisabledCoreSpecificArchive_DownloadsInstanceAssetFromDefaultArchive()
    {
        // Regression for the vectrex "Not found" bug: a core that has a (disabled) core-specific
        // archive entry — like obsidian.Vectrex -> htgdb-gamepacks — must still resolve AND download
        // its per-file instance-json assets from the DEFAULT archive where they actually live.
        // The bug downloaded from the core-specific archive (404) even though the lookup found the
        // file in the default archive.

        // Inventory + on-disk install so IsInstalled() and ReadCoreJson() succeed.
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            CoreId, PlatformId, Version, downloadUrl: _mock.BaseUrl + "/unused.zip");
        _fx.WriteInventory(new[] { inventoryCore }, new[] { new Platform
        {
            id = PlatformId, category = "Console", name = PlatformName, manufacturer = "Test", year = 1990
        } });
        _fx.WriteSettings();

        PreInstallCore();
        WriteInstanceJson();

        // Default archive index contains the asset under the platform-prefixed path; core-specific
        // archive download is intentionally NOT stubbed, so hitting it would 404 -> file not written.
        StubDefaultArchiveMetadata();
        StubDefaultArchiveDownload(out var expectedBytes);

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        // Mirror romsets.json's obsidian.Vectrex entry: a disabled core_specific_archive for this core.
        ServiceHelper.SettingsService.Config.archives.Add(new SettingsArchive
        {
            name = CoreId,
            type = ArchiveType.core_specific_archive,
            archive_name = "core-specific-pack",
            files = new List<string> { "pack.zip" },
            enabled = false,
            one_time = true,
            complete = false
        });

        // Act
        ServiceHelper.CoresService.DownloadCoreAssets(new List<Core> { inventoryCore });

        // Assert — the asset landed on disk (only possible if the download targeted the default archive).
        string installedPath = Path.Combine(_fx.PocketDir, "Assets", PlatformId, "common", "games", "rom.bin");
        File.Exists(installedPath).Should().BeTrue(
            "the instance-json asset must download from the default archive, not the disabled core-specific one");
        File.ReadAllBytes(installedPath).Should().Equal(expectedBytes);

        var paths = _mock.Server.LogEntries.Select(e => e.RequestMessage.AbsolutePath).ToList();
        paths.Should().Contain(DefaultDownloadPath, "the download must hit the default archive URL");
        paths.Should().NotContain(p => p.StartsWith("/download/core-specific-pack/"),
            "the download must NOT target the core-specific archive");
    }

    private void PreInstallCore()
    {
        string coreDir = Path.Combine(_fx.PocketDir, "Cores", CoreId);
        Directory.CreateDirectory(coreDir);
        File.WriteAllText(Path.Combine(coreDir, "core.json"),
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
        // DownloadAssets dereferences ReadDataJson(...).data.data_slots, so data.json must exist.
        File.WriteAllText(Path.Combine(coreDir, "data.json"),
            """{ "data": { "data_slots": [] } }""");
    }

    private void WriteInstanceJson()
    {
        string instanceDir = Path.Combine(_fx.PocketDir, "Assets", PlatformId, CoreId, "games");
        Directory.CreateDirectory(instanceDir);
        File.WriteAllText(Path.Combine(instanceDir, "thing.json"),
            $$"""
            {
              "instance": {
                "magic": "APF_VER_1",
                "data_path": "",
                "data_slots": [
                  { "id": "1", "filename": "{{SlotFilename}}" }
                ]
              }
            }
            """);
    }

    private void StubDefaultArchiveMetadata()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/metadata/" + DefaultArchiveName).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""
              {
                "files_count": 1,
                "item_last_updated": 0,
                "files": [
                  { "name": "test_platform/games/rom.bin" }
                ]
              }
              """));
    }

    private void StubDefaultArchiveDownload(out byte[] bytes)
    {
        bytes = "ROM_BYTES"u8.ToArray();
        _mock.Server
            .Given(Request.Create().WithPath(DefaultDownloadPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(bytes));
    }
}
