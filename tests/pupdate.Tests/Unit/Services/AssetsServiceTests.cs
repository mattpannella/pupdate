using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

// IsBlacklisted reads the blacklist.json from the current working directory when
// useLocalBlacklist=true. Tests change CWD into a temp dir to scope that lookup, then
// restore the original CWD in Dispose so they don't leave a phantom CWD for the next
// test class (which would crash WireMock's GetCwd() call later in the run).
public class AssetsServiceTests : IClassFixture<TempDirectoryFixture>, IDisposable
{
    private readonly TempDirectoryFixture _temp;
    private readonly string _origCwd;

    public AssetsServiceTests(TempDirectoryFixture temp)
    {
        _temp = temp;
        _origCwd = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        try { Directory.SetCurrentDirectory(_origCwd); } catch { /* origCwd may be gone */ }
        GC.SuppressFinalize(this);
    }

    private AssetsService BuildServiceWithBlacklist(IEnumerable<string> entries)
    {
        string scratch = Path.Combine(_temp.Path, "blacklist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, "blacklist.json"), JsonConvert.SerializeObject(entries));
        Directory.SetCurrentDirectory(scratch);
        return new AssetsService(useLocalBlacklist: true, showStackTraces: false);
    }

    [Fact]
    public void IsBlacklisted_ExactBasenameMatch_ReturnsTrue()
    {
        var svc = BuildServiceWithBlacklist(new[] { "bad.bin" });
        svc.IsBlacklisted("path/to/bad.bin").Should().BeTrue();
    }

    [Fact]
    public void IsBlacklisted_ExactFullPathMatch_ReturnsTrue()
    {
        var svc = BuildServiceWithBlacklist(new[] { "path/to/bad.bin" });
        svc.IsBlacklisted("path/to/bad.bin").Should().BeTrue();
    }

    [Fact]
    public void IsBlacklisted_StarWildcard_MatchesByExtension()
    {
        var svc = BuildServiceWithBlacklist(new[] { "*.bin" });
        svc.IsBlacklisted("foo.bin").Should().BeTrue();
        svc.IsBlacklisted("foo.txt").Should().BeFalse();
    }

    [Fact]
    public void IsBlacklisted_QuestionWildcard_MatchesSingleChar()
    {
        var svc = BuildServiceWithBlacklist(new[] { "a?.bin" });
        svc.IsBlacklisted("ab.bin").Should().BeTrue();
        svc.IsBlacklisted("abc.bin").Should().BeFalse();
    }

    [Fact]
    public void IsBlacklisted_NullOrEmpty_ReturnsFalse()
    {
        var svc = BuildServiceWithBlacklist(new[] { "anything.bin" });
        svc.IsBlacklisted(null).Should().BeFalse();
        svc.IsBlacklisted(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void IsBlacklisted_MissingBlacklistFile_ReturnsEmptyList_NoThrow()
    {
        string scratch = Path.Combine(_temp.Path, "no-blacklist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Directory.SetCurrentDirectory(scratch);

        var svc = new AssetsService(useLocalBlacklist: true, showStackTraces: false);

        svc.IsBlacklisted("anything.bin").Should().BeFalse();
        svc.Blacklist.Should().BeEmpty();
    }

    [Fact]
    public void BackupSaves_NoSavesDir_DoesNotProduceArchive()
    {
        string root = Path.Combine(_temp.Path, "root-" + Guid.NewGuid().ToString("N"));
        string backups = Path.Combine(_temp.Path, "backups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(backups);

        AssetsService.BackupSaves(root, backups);

        Directory.GetFiles(backups, "Saves_Backup_*.zip").Should().BeEmpty();
    }

    [Fact]
    public void BackupSaves_ProducesZipArchive_WhenSavesDirHasContent()
    {
        string root = Path.Combine(_temp.Path, "root-saves-" + Guid.NewGuid().ToString("N"));
        string backups = Path.Combine(_temp.Path, "backups-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Saves", "core"));
        File.WriteAllText(Path.Combine(root, "Saves", "core", "save.dat"), "save");

        AssetsService.BackupSaves(root, backups);

        Directory.GetFiles(backups, "Saves_Backup_*.zip").Should().HaveCount(1);
    }

    [Fact]
    public void BackupSaves_DedupesByContentHash()
    {
        string root = Path.Combine(_temp.Path, "root-dedupe-" + Guid.NewGuid().ToString("N"));
        string backups = Path.Combine(_temp.Path, "backups-dedupe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Saves", "core"));
        File.WriteAllText(Path.Combine(root, "Saves", "core", "save.dat"), "save");

        AssetsService.BackupSaves(root, backups);
        // Wait at least 1s so a second backup would have a different timestamp string
        // (ensures dedupe is hash-based, not name-collision-based)
        Thread.Sleep(1100);
        AssetsService.BackupSaves(root, backups);

        Directory.GetFiles(backups, "Saves_Backup_*.zip").Should().HaveCount(1,
            "second backup with identical content should be deduped via SHA256 hash");
    }

    [Fact]
    public void BackupSaves_DifferentContent_ProducesTwoArchives()
    {
        string root = Path.Combine(_temp.Path, "root-diff-" + Guid.NewGuid().ToString("N"));
        string backups = Path.Combine(_temp.Path, "backups-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Saves", "core"));
        File.WriteAllText(Path.Combine(root, "Saves", "core", "save.dat"), "v1");

        AssetsService.BackupSaves(root, backups);
        Thread.Sleep(1100);
        File.WriteAllText(Path.Combine(root, "Saves", "core", "save.dat"), "v2");
        AssetsService.BackupSaves(root, backups);

        Directory.GetFiles(backups, "Saves_Backup_*.zip").Should().HaveCount(2);
    }

    [Fact]
    public void BackupSaves_NullDirectory_Throws()
    {
        var act = () => AssetsService.BackupSaves(null, _temp.Path);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BackupSaves_NullBackupLocation_Throws()
    {
        var act = () => AssetsService.BackupSaves(_temp.Path, null);
        act.Should().Throw<ArgumentNullException>();
    }
}
