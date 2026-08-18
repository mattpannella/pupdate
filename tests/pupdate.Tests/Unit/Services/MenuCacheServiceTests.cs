using System.Text;
using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

// Locks in the reverse-engineered, hardware-confirmed openFPGA menu-cache format so future changes can't
// silently break it (wrong bytes => the Pocket rebuilds the cache on boot and, over the limit, crashes).
public class MenuCacheServiceTests : IClassFixture<TempDirectoryFixture>
{
    private const int HeaderSize = 0x1000;   // MenuCacheService.HeaderSize
    private const int BufferSize = 0x8000;   // MenuCacheService.BufferSize (the crash limit)

    private readonly TempDirectoryFixture _temp;

    public MenuCacheServiceTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private (string install, MenuCacheService svc) NewSvc(IEnumerable<string> recomputed = null)
    {
        string install = Path.Combine(_temp.Path, "pocket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(install);
        var cores = new CoresService(install, settingsService: null, archiveService: null, assetsService: null);
        return (install, new MenuCacheService(install, cores, recomputed));
    }

    private static void WriteCore(string install, string folder, string platformId,
        string author = "auth", string shortname = "short", string version = "1.0.0")
    {
        string dir = Path.Combine(install, "Cores", folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "core.json"),
            $$"""
            {
              "core": {
                "magic": "APF_VER_1",
                "metadata": {
                  "platform_ids": ["{{platformId}}"],
                  "shortname": "{{shortname}}",
                  "description": "d", "url": "u",
                  "author": "{{author}}",
                  "version": "{{version}}",
                  "date_release": "2024-01-01"
                },
                "framework": { "name": "0", "version": "0" }
              }
            }
            """);
    }

    private static void WritePlatform(string install, string id, string name,
        string category = "Console", string manufacturer = "m", int year = 2000)
    {
        string dir = Path.Combine(install, "Platforms");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id + ".json"),
            $$"""
            { "platform": { "id": "{{id}}", "category": "{{category}}", "name": "{{name}}",
                            "manufacturer": "{{manufacturer}}", "year": {{year}} } }
            """);
    }

    // corelist_cache.bin: 80-byte records; hash (uint32 LE) at 0x4c, id (null-terminated) at +4.
    private static List<(string id, uint hash)> ReadCorelist(byte[] data)
    {
        var rows = new List<(string, uint)>();

        for (int o = 0; o + 80 <= data.Length; o += 80)
        {
            int end = Array.IndexOf(data, (byte)0, o + 4, 64);
            string id = Encoding.UTF8.GetString(data, o + 4, (end < 0 ? o + 4 + 64 : end) - (o + 4));
            uint hash = BitConverter.ToUInt32(data, o + 0x4c);
            rows.Add((id, hash));
        }

        return rows;
    }

    // A reference CRC32 (independent of the production impl), anchored to the canonical check value below.
    private static uint Crc32(string s, uint seed = 0)
    {
        uint crc = seed ^ 0xFFFFFFFF;

        foreach (byte b in Encoding.UTF8.GetBytes(s))
        {
            crc ^= b;

            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    [Fact]
    public void ReferenceCrc32_MatchesCanonicalCheckValue()
    {
        // Sanity-check the test's own reference against the well-known CRC32 check vector.
        Crc32("123456789").Should().Be(0xCBF43926);
    }

    [Fact]
    public void CorelistHash_SingleCore_IsPlainCrc32OfFolderName()
    {
        var (install, svc) = NewSvc();
        WriteCore(install, "123456789", "p0");

        var rows = ReadCorelist(svc.BuildCorelistCache());

        rows.Should().ContainSingle();
        // Anchored to the canonical CRC32 check value: the hash of the first folder is a plain CRC32.
        rows[0].hash.Should().Be(0xCBF43926);
    }

    [Fact]
    public void CorelistHash_MatchesRealPocketValue()
    {
        // The real Analogue Pocket baked 0xD398FDB9 for folder "agg23.NES" (first core). Anchors to hardware.
        var (install, svc) = NewSvc();
        WriteCore(install, "agg23.NES", "nes");

        ReadCorelist(svc.BuildCorelistCache())[0].hash.Should().Be(0xD398FDB9);
    }

    [Fact]
    public void CorelistHash_IsRollingCrc32ChainedThroughDirectoryOrder()
    {
        var (install, svc) = NewSvc();

        // folder name == platform id (lowercase) so we can map each corelist row back to its folder name.
        foreach (var f in new[] { "alpha", "bravo", "charlie", "delta" })
        {
            WriteCore(install, f, f);
        }

        var rows = ReadCorelist(svc.BuildCorelistCache());
        rows.Should().HaveCount(4);

        // Each row's hash must be the rolling CRC32 of every folder name up to and including it, in the
        // order the corelist emits them (the Pocket's /Cores directory order).
        uint seed = 0;

        foreach (var (id, hash) in rows)
        {
            seed = Crc32(id, seed);
            hash.Should().Be(seed);
        }
    }

    [Fact]
    public void PlatformsCache_IsOrderedByNameCaseInsensitive()
    {
        var (install, svc) = NewSvc();
        // ids deliberately unsorted vs names; only the display name (casefold) should drive the order.
        WriteCore(install, "c0", "zoo"); WritePlatform(install, "zoo", "banana");
        WriteCore(install, "c1", "aaa"); WritePlatform(install, "aaa", "Apple");
        WriteCore(install, "c2", "mmm"); WritePlatform(install, "mmm", "cherry");

        byte[] pc = svc.BuildPlatformsCache();
        var names = new List<string>();

        for (int o = 0; o + 124 <= pc.Length; o += 124)
        {
            int end = Array.IndexOf(pc, (byte)0, o + 0x30, 32);
            names.Add(Encoding.UTF8.GetString(pc, o + 0x30, (end < 0 ? o + 0x30 + 32 : end) - (o + 0x30)));
        }

        names.Should().Equal("Apple", "banana", "cherry");
    }

    [Fact]
    public void ProjectedCoreViewSize_MatchesFormula()
    {
        // size = 4096 + 116*P + 4*M, where P = installed platforms, M = core-platform memberships.
        var (install, svc) = NewSvc();

        // platform "shared" gets two cores (+4 for the second); platform "solo" gets one; "orphan" has no core.
        WriteCore(install, "a", "shared", shortname: "a");
        WriteCore(install, "b", "shared", shortname: "b");
        WriteCore(install, "c", "solo", shortname: "c");
        WritePlatform(install, "shared", "Shared");
        WritePlatform(install, "solo", "Solo");
        WritePlatform(install, "orphan", "Orphan"); // 0 cores -> must NOT count

        int p = 2, m = 3; // installed platforms=2 (shared, solo); memberships=3 (2 on shared, 1 on solo)
        svc.ProjectedCoreViewSize().Should().Be(4096 + (116 * p) + (4 * m));
    }

    [Fact]
    public void ZeroCorePlatform_IsInFlatCacheButNotInViews()
    {
        var (install, svc) = NewSvc();
        WriteCore(install, "a", "nes");
        WritePlatform(install, "nes", "NES");
        WritePlatform(install, "orphan", "Orphan"); // json but no core

        // flat platforms_cache includes every /Platforms/*.json (both)
        (svc.BuildPlatformsCache().Length / 124).Should().Be(2);

        // the by-category view includes only installed platforms (just "nes")
        byte[] view = svc.BuildPlatformViewByCategory();
        ((view.Length - HeaderSize) / 120).Should().Be(1);
    }

    [Fact]
    public void CoresCache_RecordLayoutIsAuthorShortnameVersion()
    {
        var (install, svc) = NewSvc();
        WriteCore(install, "a", "nes", author: "agg23", shortname: "NES", version: "1.2.3");

        byte[] cc = svc.BuildCoresCache();
        cc.Length.Should().Be(96);

        string Field(int off) =>
            Encoding.UTF8.GetString(cc, off, Array.IndexOf(cc, (byte)0, off, 32) - off);

        Field(0).Should().Be("agg23");   // author
        Field(32).Should().Be("NES");    // shortname
        Field(64).Should().Be("1.2.3");  // version
    }

    [Fact]
    public void CoreViewByPlatform_IsWrittenCompleteEvenWhenOverBuffer()
    {
        // Regression guard: the view must NOT self-truncate at the 32 KB buffer. Capping is rejected by the
        // Pocket (it rebuilds), so an over-limit set must still be emitted whole (the caller decides to warn).
        var (install, svc) = NewSvc();

        for (int i = 0; i < 300; i++)
        {
            string id = $"p{i:D3}";
            WriteCore(install, id, id);
            WritePlatform(install, id, $"Platform {i:D3}");
        }

        byte[] view = svc.BuildCoreViewByPlatform();
        view.Length.Should().Be(svc.ProjectedCoreViewSize());
        view.Length.Should().BeGreaterThan(BufferSize);
    }

    // N single-core platforms => projected = 4096 + 120*N; drives GetStatus across the color bands.
    private static void WriteSinglecorePlatforms(string install, int count)
    {
        for (int i = 0; i < count; i++)
        {
            string id = $"p{i:D3}";
            WriteCore(install, id, id, shortname: id);
            WritePlatform(install, id, $"Platform {i:D3}");
        }
    }

    [Theory]
    [InlineData(3, MenuCacheService.MenuCacheLevel.Safe)]     // 4456 bytes, ~235 platforms of room
    [InlineData(225, MenuCacheService.MenuCacheLevel.Close)]  // 31096 bytes, ~13 left
    [InlineData(237, MenuCacheService.MenuCacheLevel.Danger)] // 32536 bytes, ~1 left
    [InlineData(240, MenuCacheService.MenuCacheLevel.Over)]   // 32896 bytes, over the 32768 buffer
    public void GetStatus_ClassifiesLevelByHeadroom(int platforms, MenuCacheService.MenuCacheLevel expected)
    {
        var (install, svc) = NewSvc();
        WriteSinglecorePlatforms(install, platforms);

        MenuCacheService.MenuCacheStatus status = svc.GetStatus();

        status.ProjectedSize.Should().Be(4096 + (120 * platforms));
        status.InstalledPlatforms.Should().Be(platforms);
        status.IsOverLimit.Should().Be(platforms == 240);
        status.Level.Should().Be(expected);
    }
}
