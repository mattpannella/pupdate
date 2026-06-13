using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class CoresServiceJsonTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public CoresServiceJsonTests(TempDirectoryFixture temp)
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

    // -------- ReadCoreJson --------

    [Fact]
    public void ReadCoreJson_HappyPath_ReturnsParsedCore()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "core.json"),
            """
            {
              "core": {
                "magic": "APF_VER_1",
                "metadata": {
                  "platform_ids": ["nes"],
                  "shortname": "NES",
                  "description": "Nintendo Entertainment System",
                  "author": "agg23",
                  "url": "https://example.com",
                  "version": "1.2.3",
                  "date_release": "2024-01-01"
                },
                "framework": { "name": "0", "version": "0" }
              }
            }
            """);

        var core = svc.ReadCoreJson("agg23.NES");

        core.Should().NotBeNull();
        core!.magic.Should().Be("APF_VER_1");
        core.metadata.platform_ids.Should().ContainSingle().Which.Should().Be("nes");
        core.metadata.shortname.Should().Be("NES");
        core.metadata.author.Should().Be("agg23");
        core.metadata.version.Should().Be("1.2.3");
    }

    [Fact]
    public void ReadCoreJson_MissingFile_ReturnsNull()
    {
        var (_, svc) = NewSvc();

        svc.ReadCoreJson("does.not.exist").Should().BeNull();
    }

    [Fact]
    public void ReadCoreJson_MalformedJson_Throws()
    {
        // Pin current behavior: JsonConvert throws on bad JSON; the read method does not catch.
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "bad.core", "core.json"), "not even close to json");

        var act = () => svc.ReadCoreJson("bad.core");
        act.Should().Throw<Exception>();
    }

    // -------- ReadPlatformJson --------

    [Fact]
    public void ReadPlatformJson_HappyPath_ReadsCoreJsonThenPlatformsJson()
    {
        var (install, svc) = NewSvc();

        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "core.json"),
            """
            {
              "core": {
                "magic": "APF_VER_1",
                "metadata": { "platform_ids": ["nes"], "shortname": "NES", "author": "agg23",
                              "description": "d", "url": "u", "version": "v", "date_release": "r" },
                "framework": { "name": "0", "version": "0" }
              }
            }
            """);

        WriteFile(Path.Combine(install, "Platforms", "nes.json"),
            """
            {
              "platform": {
                "id": "nes",
                "category": "Console",
                "name": "Nintendo Entertainment System",
                "manufacturer": "Nintendo",
                "year": 1983
              }
            }
            """);

        var platform = svc.ReadPlatformJson("agg23.NES");

        platform.Should().NotBeNull();
        platform!.id.Should().Be("nes");
        platform.name.Should().Be("Nintendo Entertainment System");
        platform.manufacturer.Should().Be("Nintendo");
        platform.year.Should().Be(1983);
    }

    [Fact]
    public void ReadPlatformJson_MissingCoreJson_ReturnsNull()
    {
        var (_, svc) = NewSvc();

        svc.ReadPlatformJson("missing.core").Should().BeNull();
    }

    [Fact]
    public void ReadPlatformJson_CoreJsonExistsButPlatformsJsonMissing_Throws()
    {
        // Pin current behavior: File.ReadAllText on missing platforms file throws — exact type
        // depends on whether the parent dir exists (DirectoryNotFoundException vs FileNotFoundException),
        // both of which inherit from IOException.
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "core.json"),
            """
            { "core": { "magic": "x", "metadata": { "platform_ids": ["nes"] }, "framework": { "name":"0","version":"0" } } }
            """);

        var act = () => svc.ReadPlatformJson("agg23.NES");
        act.Should().Throw<IOException>();
    }

    // -------- ReadDataJson --------

    [Fact]
    public void ReadDataJson_HappyPath_ReturnsParsedData()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "data.json"),
            """
            {
              "data": {
                "magic": "APF_VER_1",
                "data_slots": [
                  { "name": "ROM", "id": "1", "required": true, "parameters": "0x10", "extensions": ["nes"] }
                ]
              }
            }
            """);

        var data = svc.ReadDataJson("agg23.NES");

        data.Should().NotBeNull();
        data!.data.Should().NotBeNull();
        data.data.data_slots.Should().HaveCount(1);
        data.data.data_slots[0].name.Should().Be("ROM");
    }

    [Fact]
    public void ReadDataJson_MissingFile_ReturnsNull()
    {
        var (_, svc) = NewSvc();

        svc.ReadDataJson("nope").Should().BeNull();
    }

    // -------- ReadVideoJson --------

    [Fact]
    public void ReadVideoJson_HappyPath_ReturnsParsedVideo()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "video.json"),
            """
            {
              "video": {
                "magic": "APF_VER_1",
                "scaler_modes": [
                  { "width": 256, "height": 240, "aspect_w": 4, "aspect_h": 3, "rotation": 0, "mirror": 0 }
                ],
                "display_modes": [
                  { "id": "0x10" },
                  { "id": "0x20" }
                ]
              }
            }
            """);

        var video = svc.ReadVideoJson("agg23.NES");

        video.Should().NotBeNull();
        video!.magic.Should().Be("APF_VER_1");
        video.scaler_modes.Should().HaveCount(1);
        video.display_modes.Should().HaveCount(2);
        video.display_modes![0].id.Should().Be("0x10");
    }

    [Fact]
    public void ReadVideoJson_MissingFile_ReturnsNull()
    {
        var (_, svc) = NewSvc();

        svc.ReadVideoJson("nope").Should().BeNull();
    }

    [Fact]
    public void ReadVideoJson_NoDisplayModesField_ReturnsVideoWithNullDisplayModes()
    {
        // display_modes has DefaultValueHandling.Ignore — absence yields null, not [].
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "agg23.NES", "video.json"),
            """
            {
              "video": {
                "magic": "APF_VER_1",
                "scaler_modes": []
              }
            }
            """);

        var video = svc.ReadVideoJson("agg23.NES");

        video.Should().NotBeNull();
        video!.display_modes.Should().BeNull();
    }

    // -------- ReadUpdatersJson --------

    [Fact]
    public void ReadUpdatersJson_HappyPath_ParsesPreviousAndLicense()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "jotego.GG", "updaters.json"),
            """
            {
              "previous": [
                { "shortname": "GameGear", "author": "jotego", "platform_id": "gg" }
              ],
              "license": { "filename": "license.bin" }
            }
            """);

        var updaters = svc.ReadUpdatersJson("jotego.GG");

        updaters.Should().NotBeNull();
        updaters!.previous.Should().HaveCount(1);
        updaters.previous[0].shortname.Should().Be("GameGear");
        updaters.previous[0].author.Should().Be("jotego");
        updaters.previous[0].platform_id.Should().Be("gg");
        updaters.license.Should().NotBeNull();
        updaters.license.filename.Should().Be("license.bin");
    }

    [Fact]
    public void ReadUpdatersJson_OnlyLicense_PreviousIsNull()
    {
        var (install, svc) = NewSvc();
        WriteFile(Path.Combine(install, "Cores", "x.y", "updaters.json"),
            """{ "license": { "filename": "key.bin" } }""");

        var updaters = svc.ReadUpdatersJson("x.y");

        updaters!.previous.Should().BeNull();
        updaters.license.filename.Should().Be("key.bin");
    }

    [Fact]
    public void ReadUpdatersJson_MissingFile_ReturnsNull()
    {
        var (_, svc) = NewSvc();

        svc.ReadUpdatersJson("nope").Should().BeNull();
    }
}
