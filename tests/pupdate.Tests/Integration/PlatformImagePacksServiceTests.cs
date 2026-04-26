using System.IO.Compression;
using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class PlatformImagePacksServiceTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;
    private readonly string _origReleases;

    public PlatformImagePacksServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);

        _fx = new OrchestrationFixture();
        // Empty inventory + settings — Install only needs ServiceHelper.TempDirectory.
        _fx.WriteInventory(Array.Empty<Core>(), Array.Empty<Platform>());
        _fx.WriteSettings();

        _origReleases = GithubApiService.RELEASES;
        GithubApiService.RELEASES = _mock.BaseUrl + "/repos/{0}/{1}/releases";
    }

    public void Dispose()
    {
        GithubApiService.RELEASES = _origReleases;
        Console.SetIn(_origStdin);
        _fx.Dispose();
    }

    private string BuildImagePackZip(string includeImagesDir = "Platforms/_images")
    {
        string zipPath = Path.Combine(_fx.Root, "imagepack.zip");

        using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        if (includeImagesDir != null)
        {
            var entry = zip.CreateEntry($"{includeImagesDir}/nes.bin");
            using var w = new StreamWriter(entry.Open());
            w.Write("PLATFORM IMAGE BYTES");
        }

        return zipPath;
    }

    private void StubLatestRelease(string owner, string repo, params (string name, string url)[] assets)
    {
        string assetsJson = string.Join(",", assets.Select(a =>
            $$"""{ "name": "{{a.name}}", "browser_download_url": "{{a.url}}", "content_type": "application/zip", "url": "{{a.url}}" }"""));
        _mock.Server
            .Given(Request.Create().WithPath($"/repos/{owner}/{repo}/releases/latest").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                $$"""{ "tag_name": "v1.0.0", "name": "Latest", "prerelease": false, "assets": [{{assetsJson}}] }"""));
    }

    // ---------- List getter ----------

    [Fact]
    public void List_LocalFile_ReturnsParsedPacks()
    {
        // OrchestrationFixture sets CWD to WorkDir. Drop a local image_packs.json there.
        File.WriteAllText(Path.Combine(_fx.WorkDir, "image_packs.json"),
            """
            [
              { "owner": "dyreschlock", "repository": "pock-platform-images", "variant": "wood" },
              { "owner": "spiritualized1997", "repository": "openFPGA-Pocket-Image-Packs", "variant": null }
            ]
            """);

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        svc.List.Should().HaveCount(2);
        svc.List[0].owner.Should().Be("dyreschlock");
        svc.List[0].variant.Should().Be("wood");
        svc.List[1].variant.Should().BeNull();
    }

    // ---------- Install ----------

    [Fact]
    public void Install_HappyPath_CopiesImagesToPlatformsImagesDir()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        string zipPath = BuildImagePackZip();
        var zipBytes = File.ReadAllBytes(zipPath);
        string downloadUrl = _mock.BaseUrl + "/dl/imagepack.zip";

        _mock.Server
            .Given(Request.Create().WithPath("/dl/imagepack.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(zipBytes));

        StubLatestRelease("dyreschlock", "pock-platform-images",
            ("imagepack-wood.zip", downloadUrl));

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        svc.Install("dyreschlock", "pock-platform-images", "wood");

        File.Exists(Path.Combine(_fx.PocketDir, "Platforms", "_images", "nes.bin"))
            .Should().BeTrue("the image pack should be extracted into {install}/Platforms/_images");
    }

    [Fact]
    public void Install_VariantSelectsMatchingAsset()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        string zipPath = BuildImagePackZip();
        var zipBytes = File.ReadAllBytes(zipPath);
        string woodUrl = _mock.BaseUrl + "/dl/wood.zip";
        string metalUrl = _mock.BaseUrl + "/dl/metal.zip";

        // Only the wood asset should be hit. Stubbing metal too so we can verify which one
        // the service downloads.
        _mock.Server
            .Given(Request.Create().WithPath("/dl/wood.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(zipBytes));
        _mock.Server
            .Given(Request.Create().WithPath("/dl/metal.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(new byte[] { 0x00 }));

        StubLatestRelease("o", "r",
            ("imagepack-metal.zip", metalUrl),
            ("imagepack-wood.zip",  woodUrl));

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        svc.Install("o", "r", "wood");

        var paths = _mock.Server.LogEntries.Select(e => e.RequestMessage.AbsolutePath).ToList();
        paths.Should().Contain("/dl/wood.zip", "variant 'wood' should match the wood-suffixed asset");
        paths.Should().NotContain("/dl/metal.zip");
    }

    [Fact]
    public void Install_NoVariant_PicksFirstAsset()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        string zipPath = BuildImagePackZip();
        var zipBytes = File.ReadAllBytes(zipPath);
        string firstUrl = _mock.BaseUrl + "/dl/first.zip";

        _mock.Server
            .Given(Request.Create().WithPath("/dl/first.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(zipBytes));

        StubLatestRelease("o", "r",
            ("imagepack-first.zip", firstUrl),
            ("imagepack-other.zip", _mock.BaseUrl + "/dl/other.zip"));

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        svc.Install("o", "r", variant: null);

        var paths = _mock.Server.LogEntries.Select(e => e.RequestMessage.AbsolutePath).ToList();
        paths.Should().Contain("/dl/first.zip");
        paths.Should().NotContain("/dl/other.zip");
    }

    [Fact]
    public void Install_NoAssets_Throws()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        _mock.Server
            .Given(Request.Create().WithPath("/repos/o/r/releases/latest").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                """{ "tag_name": "v1.0.0", "name": "Empty", "prerelease": false, "assets": null }"""));

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        var act = () => svc.Install("o", "r", null);
        act.Should().Throw<Exception>().WithMessage("*no assets*");
    }

    [Fact]
    public void Install_VariantNotFound_Throws()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        StubLatestRelease("o", "r",
            ("imagepack-metal.zip", _mock.BaseUrl + "/dl/metal.zip"));

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        // Single() with no match throws InvalidOperationException.
        var act = () => svc.Install("o", "r", "wood");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Install_ZipMissingExpectedDirs_Throws()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        // Build a zip that has NO Platforms/_images entry at any level.
        string zipPath = Path.Combine(_fx.Root, "bad.zip");
        using (var fs = File.Create(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("README.txt");
            using var w = new StreamWriter(entry.Open());
            w.Write("no images here");
        }
        var zipBytes = File.ReadAllBytes(zipPath);

        _mock.Server
            .Given(Request.Create().WithPath("/dl/bad.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(zipBytes));

        StubLatestRelease("o", "r",
            ("imagepack-wood.zip", _mock.BaseUrl + "/dl/bad.zip"));

        var svc = new PlatformImagePacksService(_fx.PocketDir, githubToken: null, useLocalImagePacks: true);

        var act = () => svc.Install("o", "r", "wood");
        act.Should().Throw<Exception>().WithMessage("*image pack*");
    }
}
