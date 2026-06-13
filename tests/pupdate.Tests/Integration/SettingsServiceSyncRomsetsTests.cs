using FluentAssertions;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class SettingsServiceSyncRomsetsTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly TempDirectoryFixture _temp;
    private readonly string _origEndpoint;
    private readonly string _origCwd;

    public SettingsServiceSyncRomsetsTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origEndpoint = SettingsService.ROMSETS_ENDPOINT;
        SettingsService.ROMSETS_ENDPOINT = _mock.BaseUrl + "/romsets.json";
        _temp = new TempDirectoryFixture();

        // SyncRomsets reads "romsets.json" from CWD if present; otherwise it fetches remote.
        // Move CWD into a clean temp dir so a stray local file in the repo doesn't interfere.
        _origCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_temp.Path);
    }

    public void Dispose()
    {
        SettingsService.ROMSETS_ENDPOINT = _origEndpoint;
        try { Directory.SetCurrentDirectory(_origCwd); } catch { /* best effort */ }
        _temp.Dispose();
    }

    [Fact]
    public void SyncRomsets_FromRemote_AddsNewEntry_AndUpdatesExisting()
    {
        // Remote returns one new core-specific archive AND an update to the existing
        // "agg23.GameAndWatch" entry (which the default Config seeds).
        const string body = """
        [
          {
            "name": "agg23.GameAndWatch",
            "type": "core_specific_archive",
            "archive_name": "fpga-gnw-opt",
            "file_extensions": [".gnw", ".bin"]
          },
          {
            "name": "newcorp.SomeCore",
            "type": "core_specific_archive",
            "archive_name": "some-archive",
            "files": ["a.bin", "b.bin"]
          },
          {
            "name": "should-be-filtered",
            "type": "internet_archive"
          }
        ]
        """;
        _mock.Server
            .Given(Request.Create().WithPath("/romsets.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

        string settingsDir = Path.Combine(_temp.Path, "settings");
        Directory.CreateDirectory(settingsDir);
        var svc = new SettingsService(settingsDir);

        // Act
        svc.SyncRomsets();

        // Assert — new entry added, existing entry got new file_extensions, and the
        // non-core_specific_archive entry was filtered out.
        var newEntry = svc.Config.archives.FirstOrDefault(a => a.name == "newcorp.SomeCore");
        newEntry.Should().NotBeNull();
        newEntry!.files.Should().BeEquivalentTo(new[] { "a.bin", "b.bin" });

        var gnw = svc.Config.archives.FirstOrDefault(a => a.name == "agg23.GameAndWatch");
        gnw.Should().NotBeNull();
        gnw!.file_extensions.Should().BeEquivalentTo(new[] { ".gnw", ".bin" },
            "existing core_specific_archive entries should be updated, not duplicated");

        svc.Config.archives.Should().NotContain(a => a.name == "should-be-filtered",
            "non-core_specific_archive entries must be filtered out");
    }

    [Fact]
    public void SyncRomsets_LocalFileWins_OverRemote()
    {
        // Place a local romsets.json in CWD; remote endpoint should NOT be hit.
        File.WriteAllText(Path.Combine(_temp.Path, "romsets.json"),
            """
            [
              { "name": "local.only", "type": "core_specific_archive", "archive_name": "local" }
            ]
            """);
        // Stub remote with different content so we can detect if the wrong source was used.
        _mock.Server
            .Given(Request.Create().WithPath("/romsets.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));

        string settingsDir = Path.Combine(_temp.Path, "settings");
        Directory.CreateDirectory(settingsDir);
        var svc = new SettingsService(settingsDir);

        svc.SyncRomsets();

        svc.Config.archives.Should().Contain(a => a.name == "local.only");
        _mock.Server.LogEntries.Should().BeEmpty(
            "local romsets.json must short-circuit before any HTTP call");
    }

    [Fact]
    public void SyncRomsets_RemoteFails_Throws()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/romsets.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        string settingsDir = Path.Combine(_temp.Path, "settings");
        Directory.CreateDirectory(settingsDir);
        var svc = new SettingsService(settingsDir);

        var act = () => svc.SyncRomsets();
        act.Should().Throw<Exception>().WithMessage("*romsets*");
    }

    [Fact]
    public void SyncRomsets_MalformedJson_Throws()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/romsets.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("not json"));

        string settingsDir = Path.Combine(_temp.Path, "settings");
        Directory.CreateDirectory(settingsDir);
        var svc = new SettingsService(settingsDir);

        var act = () => svc.SyncRomsets();
        act.Should().Throw<Exception>().WithMessage("*deserialize*");
    }

    [Fact]
    public void SyncRomsets_PersistsAcrossReload()
    {
        const string body = """
        [
          { "name": "round.trip.core", "type": "core_specific_archive",
            "archive_name": "round-trip", "files": ["x.bin"] }
        ]
        """;
        _mock.Server
            .Given(Request.Create().WithPath("/romsets.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

        string settingsDir = Path.Combine(_temp.Path, "settings");
        Directory.CreateDirectory(settingsDir);
        var svc = new SettingsService(settingsDir);
        svc.SyncRomsets();

        // Reload — synced archive must persist via Save().
        var reloaded = new SettingsService(settingsDir);
        reloaded.Config.archives.Should().Contain(a => a.name == "round.trip.core");
    }
}
