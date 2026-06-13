using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class CoresServiceReplaceTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public CoresServiceReplaceTests(TempDirectoryFixture temp)
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

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void GetSubstitutes_NoUpdatersFile_ReturnsNull()
    {
        var (_, svc) = NewSvc();

        svc.GetSubstitutes("nope").Should().BeNull();
    }

    [Fact]
    public void GetSubstitutes_NoPreviousField_ReturnsNull()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "x.y", "updaters.json"),
            """{ "license": { "filename": "key.bin" } }""");

        svc.GetSubstitutes("x.y").Should().BeNull();
    }

    [Fact]
    public void GetSubstitutes_HappyPath_ReturnsArray()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "new.author.NES", "updaters.json"),
            """
            {
              "previous": [
                { "shortname": "NES", "author": "old",   "platform_id": "nes" },
                { "shortname": "NES", "author": "older", "platform_id": "nes" }
              ]
            }
            """);

        var subs = svc.GetSubstitutes("new.author.NES");

        subs.Should().NotBeNull();
        subs!.Should().HaveCount(2);
        subs[0].author.Should().Be("old");
        subs[0].shortname.Should().Be("NES");
        subs[0].platform_id.Should().Be("nes");
        subs[1].author.Should().Be("older");
    }
}
