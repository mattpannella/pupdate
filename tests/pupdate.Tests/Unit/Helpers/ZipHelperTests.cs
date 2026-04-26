using System.IO.Compression;
using FluentAssertions;
using Pannella.Helpers;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Helpers;

public class ZipHelperTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public ZipHelperTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private string MakeCleanZip(string name)
    {
        string zipPath = Path.Combine(_temp.Path, name);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        var fileEntry = archive.CreateEntry("hello.txt");
        using (var writer = new StreamWriter(fileEntry.Open()))
        {
            writer.Write("hello");
        }

        var nestedEntry = archive.CreateEntry("sub/nested.txt");
        using (var writer = new StreamWriter(nestedEntry.Open()))
        {
            writer.Write("nested");
        }

        return zipPath;
    }

    [Fact]
    public void ExtractToDirectory_HappyPath_ExtractsAllEntries()
    {
        string zip = MakeCleanZip("clean-" + Guid.NewGuid().ToString("N") + ".zip");
        string dest = Path.Combine(_temp.Path, "extract-" + Guid.NewGuid().ToString("N"));

        ZipHelper.ExtractToDirectory(zip, dest, overwrite: false, useProgress: false);

        File.Exists(Path.Combine(dest, "hello.txt")).Should().BeTrue();
        File.Exists(Path.Combine(dest, "sub", "nested.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(dest, "hello.txt")).Should().Be("hello");
    }

    [Fact]
    public void ExtractToDirectory_PathTraversalEntry_Throws()
    {
        // Create a zip with a path traversal entry. Need to bypass ZipArchive sanitization
        // by writing the local file header manually... but ZipArchive does allow "../foo"
        // as an entry name even though it sanitizes during extraction. Use that.
        string zipPath = Path.Combine(_temp.Path, "traversal-" + Guid.NewGuid().ToString("N") + ".zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("pwned");
        }

        string dest = Path.Combine(_temp.Path, "extract-traversal-" + Guid.NewGuid().ToString("N"));

        var act = () => ZipHelper.ExtractToDirectory(zipPath, dest, overwrite: false, useProgress: false);
        act.Should().Throw<IOException>().WithMessage("*outside of the folder*");
    }

    [Fact]
    public void ExtractToDirectory_OverwriteFalse_OnExistingFile_Throws()
    {
        string zip = MakeCleanZip("clean-" + Guid.NewGuid().ToString("N") + ".zip");
        string dest = Path.Combine(_temp.Path, "extract-no-overwrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, "hello.txt"), "existing");

        var act = () => ZipHelper.ExtractToDirectory(zip, dest, overwrite: false, useProgress: false);
        act.Should().Throw<IOException>();
    }

    [Fact]
    public void ExtractToDirectory_OverwriteTrue_ReplacesExistingFile()
    {
        string zip = MakeCleanZip("clean-" + Guid.NewGuid().ToString("N") + ".zip");
        string dest = Path.Combine(_temp.Path, "extract-overwrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, "hello.txt"), "existing");

        ZipHelper.ExtractToDirectory(zip, dest, overwrite: true, useProgress: false);

        File.ReadAllText(Path.Combine(dest, "hello.txt")).Should().Be("hello");
    }
}
