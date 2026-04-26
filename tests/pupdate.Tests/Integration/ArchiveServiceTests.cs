using FluentAssertions;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using SettingsArchive = Pannella.Models.Settings.Archive;
using ArchiveFile = Pannella.Models.Archive.File;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class ArchiveServiceTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly TempDirectoryFixture _temp;
    private readonly string _origMetadata;
    private readonly string _origDownload;

    public ArchiveServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origMetadata = ArchiveService.METADATA;
        _origDownload = ArchiveService.DOWNLOAD;
        ArchiveService.METADATA = _mock.BaseUrl + "/metadata/{0}";
        ArchiveService.DOWNLOAD = _mock.BaseUrl + "/download/{0}/{1}";
        _temp = new TempDirectoryFixture();
    }

    public void Dispose()
    {
        ArchiveService.METADATA = _origMetadata;
        ArchiveService.DOWNLOAD = _origDownload;
        _temp.Dispose();
    }

    private static SettingsArchive InternetArchiveOf(string name) => new SettingsArchive
    {
        name = "default",
        type = ArchiveType.internet_archive,
        archive_name = name
    };

    private static List<SettingsArchive> Archives(SettingsArchive a) => new() { a };

    private ArchiveService Build(SettingsArchive archive, bool crc = false, bool cache = false)
    {
        return new ArchiveService(
            archives: Archives(archive),
            credentials: null,
            crcCheck: crc,
            useCustomArchive: false,
            showStackTraces: false,
            cacheArchiveFiles: cache,
            cacheDirectory: Path.Combine(_temp.Path, "cache"));
    }

    [Fact]
    public void GetArchiveFiles_ReturnsParsedFiles_FromMetadataEndpoint()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/metadata/openFPGA-Files").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""
              {
                "files_count": 2,
                "item_last_updated": 0,
                "files": [
                  { "name": "rom_a.bin", "md5": "aaaa", "crc32": "deadbeef" },
                  { "name": "rom_b.bin", "md5": "bbbb", "crc32": "12345678" }
                ]
              }
              """));

        var svc = Build(InternetArchiveOf("openFPGA-Files"));
        var files = svc.GetArchiveFiles("default").ToList();

        files.Should().HaveCount(2);
        files[0].name.Should().Be("rom_a.bin");
        files[1].crc32.Should().Be("12345678");
    }

    [Fact]
    public void GetArchiveFiles_FilterByExtension_NarrowsResults()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/metadata/ext-filter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""
              { "files_count": 3, "item_last_updated": 0, "files": [
                { "name": "rom.bin" }, { "name": "doc.txt" }, { "name": "rom2.BIN" } ] }
              """));

        var archive = new SettingsArchive
        {
            name = "default",
            type = ArchiveType.internet_archive,
            archive_name = "ext-filter",
            file_extensions = new List<string> { ".bin" }
        };
        var svc = Build(archive);
        var files = svc.GetArchiveFiles("default").Select(f => f.name).ToList();

        files.Should().BeEquivalentTo(new[] { "rom.bin", "rom2.BIN" });
    }

    [Fact]
    public void GetArchiveFiles_FilterByExplicitFileList_NarrowsResults()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/metadata/file-filter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""
              { "files_count": 3, "item_last_updated": 0, "files": [
                { "name": "wanted.bin" }, { "name": "ignored.bin" }, { "name": "wanted2.bin" } ] }
              """));

        var archive = new SettingsArchive
        {
            name = "default",
            type = ArchiveType.internet_archive,
            archive_name = "file-filter",
            files = new List<string> { "wanted.bin", "wanted2.bin" }
        };
        var svc = Build(archive);
        var files = svc.GetArchiveFiles("default").Select(f => f.name).ToList();

        files.Should().BeEquivalentTo(new[] { "wanted.bin", "wanted2.bin" });
    }

    [Fact]
    public void GetArchiveFiles_InternetArchive_404_Throws()
    {
        // Pin current behavior: GetFiles (non-custom) does NOT catch HttpRequestException,
        // so a 404 from archive.org/metadata propagates. Only custom archives catch.
        _mock.Server
            .Given(Request.Create().WithPath("/metadata/missing").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var svc = Build(InternetArchiveOf("missing"));
        var act = () => svc.GetArchiveFiles("default").ToList();

        act.Should().Throw<HttpRequestException>();
    }

    [Fact]
    public void DownloadArchiveFile_HappyPath_WritesFile()
    {
        var content = "ROM_CONTENTS"u8.ToArray();
        _mock.Server
            .Given(Request.Create().WithPath("/download/dl-test/file.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(content));

        var archive = InternetArchiveOf("dl-test");
        var svc = Build(archive);
        var file = new ArchiveFile { name = "file.bin", md5 = "ignored", crc32 = "ignored" };

        Directory.CreateDirectory(Path.Combine(_temp.Path, "out"));
        var ok = svc.DownloadArchiveFile(archive, file, Path.Combine(_temp.Path, "out"));

        ok.Should().BeTrue();
        File.ReadAllBytes(Path.Combine(_temp.Path, "out", "file.bin")).Should().Equal(content);
    }

    [Fact]
    public void DownloadArchiveFile_404_ReturnsFalse()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/download/dl-404/missing.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var archive = InternetArchiveOf("dl-404");
        var svc = Build(archive);
        var file = new ArchiveFile { name = "missing.bin" };

        Directory.CreateDirectory(Path.Combine(_temp.Path, "out"));
        var ok = svc.DownloadArchiveFile(archive, file, Path.Combine(_temp.Path, "out"));

        ok.Should().BeFalse();
    }

    [Fact]
    public void DownloadArchiveFile_NullArchiveOrFile_ReturnsFalse()
    {
        var svc = Build(InternetArchiveOf("noop"));
        svc.DownloadArchiveFile(null, new ArchiveFile { name = "x" }, _temp.Path).Should().BeFalse();
        svc.DownloadArchiveFile(InternetArchiveOf("noop"), null, _temp.Path).Should().BeFalse();
    }

    [Fact]
    public void DownloadArchiveFile_CacheHit_CopiesFromCache_NoNetworkCall()
    {
        var content = "CACHED"u8.ToArray();
        var md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(content));

        var archive = InternetArchiveOf("cache-hit");
        // Pre-populate cache at expected location: {cacheDir}/{archive_name}/{file.name}
        var cachedPath = Path.Combine(_temp.Path, "cache", "cache-hit", "cached.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(cachedPath)!);
        File.WriteAllBytes(cachedPath, content);

        var svc = Build(archive, cache: true);
        var file = new ArchiveFile { name = "cached.bin", md5 = md5 };

        string outDir = Path.Combine(_temp.Path, "out-cache");
        Directory.CreateDirectory(outDir);

        var ok = svc.DownloadArchiveFile(archive, file, outDir);

        ok.Should().BeTrue();
        File.ReadAllBytes(Path.Combine(outDir, "cached.bin")).Should().Equal(content);
        _mock.Server.LogEntries.Should().BeEmpty(
            "cache hit must short-circuit before any HTTP call");
    }

    [Fact]
    public void DownloadArchiveFile_CacheMiss_DownloadsAndPopulatesCache()
    {
        var content = "FRESH"u8.ToArray();
        var md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(content));

        _mock.Server
            .Given(Request.Create().WithPath("/download/cache-miss/fresh.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(content));

        var archive = InternetArchiveOf("cache-miss");
        var svc = Build(archive, cache: true);
        var file = new ArchiveFile { name = "fresh.bin", md5 = md5 };

        string outDir = Path.Combine(_temp.Path, "out-miss");
        Directory.CreateDirectory(outDir);

        svc.DownloadArchiveFile(archive, file, outDir).Should().BeTrue();

        var cachedPath = Path.Combine(_temp.Path, "cache", "cache-miss", "fresh.bin");
        File.Exists(cachedPath).Should().BeTrue("cache should be populated after successful download");
        File.ReadAllBytes(cachedPath).Should().Equal(content);
    }

    [Fact]
    public void DownloadArchiveFile_CrcMismatch_RetriesAndReturnsTrue_WhenEventuallyValid()
    {
        // Sequence: bad bytes twice, then good bytes the third time.
        var goodBytes = "PERFECT"u8.ToArray();
        var goodCrc = ComputeCrc32Hex(goodBytes);

        var queue = new Queue<byte[]>();
        queue.Enqueue("BAD1"u8.ToArray());
        queue.Enqueue("BAD2"u8.ToArray());
        queue.Enqueue(goodBytes);

        _mock.Server
            .Given(Request.Create().WithPath("/download/crc-retry/file.bin").UsingGet())
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                var bytes = queue.Count > 0 ? queue.Dequeue() : goodBytes;
                return new WireMock.ResponseMessage
                {
                    StatusCode = 200,
                    BodyData = new WireMock.Util.BodyData
                    {
                        DetectedBodyType = WireMock.Types.BodyType.Bytes,
                        BodyAsBytes = bytes
                    }
                };
            }));

        var archive = InternetArchiveOf("crc-retry");
        var svc = Build(archive, crc: true);
        var file = new ArchiveFile { name = "file.bin", crc32 = goodCrc };

        string outDir = Path.Combine(_temp.Path, "out-crc");
        Directory.CreateDirectory(outDir);

        var ok = svc.DownloadArchiveFile(archive, file, outDir);

        ok.Should().BeTrue();
        ComputeCrc32Hex(File.ReadAllBytes(Path.Combine(outDir, "file.bin"))).Should().Be(goodCrc);
    }

    private static string ComputeCrc32Hex(byte[] bytes)
    {
        var crc = new Force.Crc32.Crc32Algorithm();
        return Convert.ToHexString(crc.ComputeHash(bytes));
    }
}
