using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class CoresServicePlatformsTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public CoresServicePlatformsTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private (string installPath, CoresService svc) NewSvc()
    {
        string installPath = Path.Combine(_temp.Path, "pocket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installPath);
        var svc = new CoresService(installPath, settingsService: null, archiveService: null, assetsService: null);
        return (installPath, svc);
    }

    private static void WritePlatform(string install, string id, string name, bool archived = false)
    {
        string dir = archived
            ? Path.Combine(install, "Platforms", "_archive")
            : Path.Combine(install, "Platforms");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id + ".json"),
            $$"""
            {
              "platform": {
                "id": "{{id}}",
                "category": "Console",
                "name": "{{name}}",
                "manufacturer": "Acme",
                "year": 1990
              }
            }
            """);
    }

    private static void WriteCore(string install, string identifier, string platformId)
    {
        string dir = Path.Combine(install, "Cores", identifier);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "core.json"),
            $$"""
            { "core": { "magic": "x", "metadata": { "platform_ids": ["{{platformId}}"] },
              "framework": { "name":"0","version":"0" } } }
            """);
    }

    [Fact]
    public void GetPlatforms_ReturnsActiveAndArchived_WithCorrectFlags()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");
        WritePlatform(install, "gb", "Game Boy", archived: true);

        var platforms = svc.GetPlatforms();

        platforms.Should().HaveCount(2);

        var nes = platforms.Single(p => p.Id == "nes");
        nes.Name.Should().Be("Nintendo");
        nes.Archived.Should().BeFalse();

        var gb = platforms.Single(p => p.Id == "gb");
        gb.Name.Should().Be("Game Boy");
        gb.Archived.Should().BeTrue();
    }

    [Fact]
    public void GetPlatforms_MarksHasInstalledCore_OnlyForReferencedPlatforms()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");
        WritePlatform(install, "gb", "Game Boy");
        WriteCore(install, "agg23.NES", "nes");

        var platforms = svc.GetPlatforms();

        platforms.Single(p => p.Id == "nes").HasInstalledCore.Should().BeTrue();
        platforms.Single(p => p.Id == "gb").HasInstalledCore.Should().BeFalse();
    }

    [Fact]
    public void GetPlatforms_EmptyWhenNoPlatformsFolder()
    {
        var (_, svc) = NewSvc();

        svc.GetPlatforms().Should().BeEmpty();
    }

    [Fact]
    public void GetPlatforms_SkipsCoreWithCorruptCoreJson_DoesNotThrow()
    {
        // A single corrupt core.json must not take down the whole platforms feature.
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");
        WriteCore(install, "agg23.NES", "nes");

        // a second installed core with malformed core.json
        string badDir = Path.Combine(install, "Cores", "bad.core");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "core.json"), "not even close to json");

        var platforms = svc.GetPlatforms();

        platforms.Should().ContainSingle(p => p.Id == "nes");
        platforms.Single(p => p.Id == "nes").HasInstalledCore.Should().BeTrue();
    }

    [Fact]
    public void ArchivePlatform_MovesActiveFileIntoArchiveFolder()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");

        svc.ArchivePlatform("nes");

        File.Exists(Path.Combine(install, "Platforms", "nes.json")).Should().BeFalse();
        File.Exists(Path.Combine(install, "Platforms", "_archive", "nes.json")).Should().BeTrue();
    }

    [Fact]
    public void ArchivePlatform_NoOp_WhenPlatformNotActive()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "gb", "Game Boy", archived: true);

        svc.ArchivePlatform("gb"); // already archived, nothing to move

        File.Exists(Path.Combine(install, "Platforms", "gb.json")).Should().BeFalse();
        File.Exists(Path.Combine(install, "Platforms", "_archive", "gb.json")).Should().BeTrue();
    }

    [Fact]
    public void UnarchivePlatform_MovesFileBackToTopLevel()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "gb", "Game Boy", archived: true);

        svc.UnarchivePlatform("gb");

        File.Exists(Path.Combine(install, "Platforms", "_archive", "gb.json")).Should().BeFalse();
        File.Exists(Path.Combine(install, "Platforms", "gb.json")).Should().BeTrue();
    }

    [Fact]
    public void ArchiveThenUnarchive_RoundTrips_PreservingContent()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");
        string original = File.ReadAllText(Path.Combine(install, "Platforms", "nes.json"));

        svc.ArchivePlatform("nes");
        svc.UnarchivePlatform("nes");

        File.Exists(Path.Combine(install, "Platforms", "nes.json")).Should().BeTrue();
        File.ReadAllText(Path.Combine(install, "Platforms", "nes.json")).Should().Be(original);
    }

    // -------- path resolution helpers --------

    [Fact]
    public void GetPlatformFilePath_PrefersActiveFile_WhenItExists()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");
        WritePlatform(install, "nes", "Nintendo", archived: true);

        svc.GetPlatformFilePath("nes")
            .Should().Be(Path.Combine(install, "Platforms", "nes.json"));
    }

    [Fact]
    public void GetPlatformFilePath_FallsBackToArchive_WhenOnlyArchivedExists()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo", archived: true);

        svc.GetPlatformFilePath("nes")
            .Should().Be(Path.Combine(install, "Platforms", "_archive", "nes.json"));
    }

    [Fact]
    public void GetPlatformFilePath_DefaultsToActivePath_WhenNeitherExists()
    {
        var (install, svc) = NewSvc();

        svc.GetPlatformFilePath("nes")
            .Should().Be(Path.Combine(install, "Platforms", "nes.json"));
    }

    [Fact]
    public void IsPlatformArchived_TrueOnlyWhenArchivedExistsAndActiveDoesNot()
    {
        var (install, svc) = NewSvc();

        // neither exists
        svc.IsPlatformArchived("nes").Should().BeFalse();

        // only archived exists
        WritePlatform(install, "nes", "Nintendo", archived: true);
        svc.IsPlatformArchived("nes").Should().BeTrue();

        // both exist -> not considered archived (active takes precedence)
        WritePlatform(install, "nes", "Nintendo");
        svc.IsPlatformArchived("nes").Should().BeFalse();
    }

    // -------- re-archive helpers (install / pocket-extras un-archive guard) --------

    [Fact]
    public void GetArchivedPlatformIds_ReturnsOnlyArchivedIds()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");                 // active
        WritePlatform(install, "gb", "Game Boy", archived: true);  // archived
        WritePlatform(install, "snes", "Super NES", archived: true); // archived

        svc.GetArchivedPlatformIds().Should().BeEquivalentTo(new[] { "gb", "snes" });
    }

    [Fact]
    public void GetArchivedPlatformIds_EmptyWhenNoArchiveFolder()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");

        svc.GetArchivedPlatformIds().Should().BeEmpty();
    }

    [Fact]
    public void ReArchivePlatforms_MovesReCreatedActiveFilesBackToArchive()
    {
        // Simulates an install that re-created an archived platform's JSON in the top-level
        // folder: snapshot before, then re-archive after.
        var (install, svc) = NewSvc();
        WritePlatform(install, "gb", "Game Boy", archived: true);

        var archivedBefore = svc.GetArchivedPlatformIds();

        // an install copies a fresh top-level gb.json into place (un-archiving it)
        WritePlatform(install, "gb", "Game Boy v2");

        svc.ReArchivePlatforms(archivedBefore);

        File.Exists(Path.Combine(install, "Platforms", "gb.json")).Should().BeFalse();
        string archived = Path.Combine(install, "Platforms", "_archive", "gb.json");
        File.Exists(archived).Should().BeTrue();
        File.ReadAllText(archived).Should().Contain("Game Boy v2"); // archived copy refreshed
    }

    [Fact]
    public void ReArchivePlatforms_NoOp_WhenPlatformNotReCreated()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "gb", "Game Boy", archived: true);

        // install did not touch this platform; it stays archived, untouched
        svc.ReArchivePlatforms(new[] { "gb" });

        File.Exists(Path.Combine(install, "Platforms", "gb.json")).Should().BeFalse();
        File.Exists(Path.Combine(install, "Platforms", "_archive", "gb.json")).Should().BeTrue();
    }

    [Fact]
    public void ArchiveUnusedPlatforms_ArchivesOnlyPlatformsWithoutInstalledCore()
    {
        var (install, svc) = NewSvc();
        WritePlatform(install, "nes", "Nintendo");   // used
        WritePlatform(install, "gb", "Game Boy");    // unused
        WritePlatform(install, "snes", "Super NES"); // unused
        WriteCore(install, "agg23.NES", "nes");

        int archived = svc.ArchiveUnusedPlatforms();

        archived.Should().Be(2);
        File.Exists(Path.Combine(install, "Platforms", "nes.json")).Should().BeTrue();
        File.Exists(Path.Combine(install, "Platforms", "_archive", "gb.json")).Should().BeTrue();
        File.Exists(Path.Combine(install, "Platforms", "_archive", "snes.json")).Should().BeTrue();
    }
}
