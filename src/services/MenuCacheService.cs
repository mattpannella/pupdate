using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella.Services;

// (Re)generates the Analogue Pocket's on-SD menu cache under /System so the Pocket accepts it as fresh and
// skips the slow post-update rebuild (the "freeze"). Format fully reverse-engineered and hardware-confirmed:
//   corelist_cache.bin  - 80B/core in /Cores directory order; the hash @0x4c is a rolling CRC32 of the
//                         folder names (seed 0, chained) == CRC32 of every folder name concatenated.
//   cores_cache.bin     - 96B/core, same order (author, shortname, version).
//   platforms_cache.bin - 124B per /Platforms/*.json, ordered alphabetically by display name (case-insensitive).
//   *_viewby_*.bin       - grouped views in a fixed 0x8000 (32768) byte buffer (0x1000 header + 120B records).
//                         When the by-platform view would exceed the buffer the Pocket's own builder overruns
//                         and crashes (QR code); that is NOT pre-buildable (the Pocket rebuilds the view on
//                         boot regardless), so the caller must keep the card under the limit / warn instead.
// FAT mtimes are preserved from the existing cache for unchanged files (correct across the macOS exFAT quirk)
// and recomputed only for files pupdate wrote this run.
public class MenuCacheService
{
    public const int HeaderSize = 0x1000;   // 4096 - reserved header region at the top of a view file
    public const int BufferSize = 0x8000;   // 32768 - the fixed buffer the Pocket builds the view in
    public const int ViewRecordSize = 120;

    private const int IdFieldSize = 16;
    private const int CategoryFieldSize = 32;
    private const int NameFieldSize = 32;
    private const int ManufacturerFieldSize = 32;

    private static readonly Encoding CacheEncoding = new UTF8Encoding(false);

    private readonly string installPath;
    private readonly CoresService coresService;

    // Folders (under /Cores) whose files pupdate (re)wrote this run. Only these get their FAT mtime
    // recomputed from disk; every other core keeps the timestamp the Pocket already baked (which is correct
    // regardless of the host OS's exFAT quirks). Empty/null => recompute only cores the Pocket never cached.
    private readonly HashSet<string> recomputedFolders;

    public MenuCacheService(string installPath, CoresService coresService,
        IEnumerable<string> recomputedFolders = null)
    {
        this.installPath = installPath;
        this.coresService = coresService;
        this.recomputedFolders = new HashSet<string>(recomputedFolders ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);
    }

    public string SystemDirectory => Path.Combine(this.installPath, "System");

    // Builds platform_viewby_category.bin: a 4096-byte header index (one 4-byte entry per category) followed
    // by 120-byte records grouped by category (categories ordinal order; within a group by index). The record
    // index is the platform's rank among installed platforms in platforms_cache order. Always the complete view.
    public byte[] BuildPlatformViewByCategory()
    {
        CacheModel model = this.BuildModel();

        // installed platforms in master order; index = rank among installed in that order
        var installed = model.Platforms.Where(p => p.Installed).ToList();
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < installed.Count; i++)
        {
            indexById[installed[i].Id] = i;
        }

        // group by category (categories listed in ordinal order); within a group, order by the index
        var fileOrder = installed
            .GroupBy(p => p.Category ?? string.Empty)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.OrderBy(p => indexById[p.Id]))
            .ToList();

        byte[] buffer = new byte[HeaderSize + (fileOrder.Count * ViewRecordSize)];

        int headerOffset = 0;
        int groupRecordOffset = HeaderSize;

        foreach (var group in fileOrder.GroupBy(p => p.Category ?? string.Empty))
        {
            int count = group.Count();
            uint entry = (uint)((((uint)groupRecordOffset / 4) << 12) | ((uint)count & 0xFFF));
            WriteUInt32(buffer, headerOffset, entry);
            headerOffset += 4;
            groupRecordOffset += count * ViewRecordSize;
        }

        int recordOffset = HeaderSize;

        foreach (var platform in fileOrder)
        {
            WriteCommonStrings(buffer, recordOffset, platform);
            WriteUInt32(buffer, recordOffset + 0x74, (uint)indexById[platform.Id]);
            recordOffset += ViewRecordSize;
        }

        return buffer;
    }

    // Builds core_viewby_platform.bin: header entry per installed platform (count = owning cores), then a
    // 120-byte platform record whose index is the first owning core's index, followed by 4 bytes per extra
    // owning core (its index). Grouped in master platform order. cap limits the number of platform groups.
    public byte[] BuildCoreViewByPlatform()
    {
        CacheModel model = this.BuildModel();
        var installed = model.Platforms.Where(p => p.Installed).ToList();

        // This is the view that overruns and crashes the Pocket (it grows on both the core and platform axes).
        // Emit every installed platform (complete view); the caller must check ProjectedCoreViewSize against
        // the buffer first, since an over-limit set can't be pre-built around (the Pocket rebuilds and crashes).
        var included = new List<(PlatformRow platform, List<int> cores)>();
        int size = HeaderSize;

        foreach (PlatformRow platform in installed)
        {
            List<int> cores = model.OwningCoreIndexes(platform.Id);
            included.Add((platform, cores));
            size += ViewRecordSize + (4 * Math.Max(0, cores.Count - 1));
        }

        byte[] buffer = new byte[size];
        int headerOffset = 0;
        int recordOffset = HeaderSize;

        foreach (var (platform, cores) in included)
        {
            int count = Math.Max(1, cores.Count);
            uint entry = (uint)((((uint)recordOffset / 4) << 12) | ((uint)count & 0xFFF));
            WriteUInt32(buffer, headerOffset, entry);
            headerOffset += 4;

            WriteCommonStrings(buffer, recordOffset, platform);
            WriteUInt32(buffer, recordOffset + 0x74, (uint)(cores.Count > 0 ? cores[0] : 0));
            recordOffset += ViewRecordSize;

            for (int i = 1; i < cores.Count; i++)
            {
                WriteUInt32(buffer, recordOffset, (uint)cores[i]);
                recordOffset += 4;
            }
        }

        return buffer;
    }

    // Full (uncapped) size core_viewby_platform.bin would occupy for the current install. When this exceeds
    // the 32 KB buffer, the Pocket's own builder overruns it and crashes - this is the crash predictor.
    public int ProjectedCoreViewSize()
    {
        CacheModel model = this.BuildModel();

        return HeaderSize + model.Platforms
            .Where(p => p.Installed)
            .Sum(p => ViewRecordSize + (4 * Math.Max(0, model.OwningCoreIndexes(p.Id).Count - 1)));
    }

    // Builds platforms_cache.bin: 124-byte records for every active platform (master order) + 1 trailing byte.
    public byte[] BuildPlatformsCache()
    {
        CacheModel model = this.BuildModel();
        byte[] buffer = new byte[(model.Platforms.Count * 124) + 1];
        int offset = 0;

        foreach (var platform in model.Platforms)
        {
            WriteCommonStrings(buffer, offset, platform);
            WriteUInt16(buffer, offset + 0x74, platform.FatTime);
            WriteUInt16(buffer, offset + 0x76, platform.FatDate);
            WriteUInt32(buffer, offset + 0x78, platform.JsonSize);
            offset += 124;
        }

        buffer[buffer.Length - 1] = model.PlatformsCacheTrailingByte;

        return buffer;
    }

    // Builds cores_cache.bin: 96-byte records (author, shortname, version) in core order.
    public byte[] BuildCoresCache()
    {
        CacheModel model = this.BuildModel();
        byte[] buffer = new byte[model.Cores.Count * 96];
        int offset = 0;

        foreach (var core in model.Cores)
        {
            WriteString(buffer, offset, core.Author, 32);
            WriteString(buffer, offset + 32, core.Shortname, 32);
            WriteString(buffer, offset + 64, core.Version, 32);
            offset += 96;
        }

        return buffer;
    }

    // Builds corelist_cache.bin: 80-byte records {index, id (=platform id), FAT mtime, core.json size, hash}.
    public byte[] BuildCorelistCache()
    {
        CacheModel model = this.BuildModel();
        byte[] buffer = new byte[model.Cores.Count * 80];
        int offset = 0;

        for (int i = 0; i < model.Cores.Count; i++)
        {
            CoreRow core = model.Cores[i];
            WriteUInt32(buffer, offset, (uint)i);
            WriteString(buffer, offset + 4, core.CorelistId, 64);
            WriteUInt16(buffer, offset + 0x44, core.FatTime);
            WriteUInt16(buffer, offset + 0x46, core.FatDate);
            WriteUInt32(buffer, offset + 0x48, core.JsonSize);
            WriteUInt32(buffer, offset + 0x4c, core.HashB);
            offset += 80;
        }

        return buffer;
    }

    // Writes the id (16) / category (32) / name (32) / manufacturer (32) / year (u32 @0x70) block shared by
    // the flat platforms_cache record and both grouped-view records.
    private static void WriteCommonStrings(byte[] buffer, int offset, PlatformRow platform)
    {
        WriteIdField(buffer, offset, platform.Id);
        WriteString(buffer, offset + IdFieldSize, platform.Category, CategoryFieldSize);
        WriteString(buffer, offset + IdFieldSize + CategoryFieldSize, platform.Name, NameFieldSize);
        WriteString(buffer, offset + IdFieldSize + CategoryFieldSize + NameFieldSize, platform.Manufacturer,
            ManufacturerFieldSize);
        WriteUInt32(buffer, offset + 0x70, (uint)platform.Year);
    }

    // The id field is the platform id and the string "json" (the source extension) as two null-terminated
    // strings packed into 16 bytes, e.g. "arduboy\0json\0". Long ids truncate the tail (matching the Pocket).
    private static void WriteIdField(byte[] buffer, int offset, string id)
    {
        byte[] bytes = CacheEncoding.GetBytes((id ?? string.Empty) + "\0" + "json");
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, IdFieldSize));
    }

    private static void WriteString(byte[] buffer, int offset, string value, int fieldSize)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        byte[] bytes = CacheEncoding.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, fieldSize));
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static readonly bool IsMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    // The FAT (date, time) the Pocket will read from a file's exFAT directory entry. The Pocket reads the raw
    // local-timestamp field and ignores the exFAT UtcOffset byte. On macOS that field is written with the
    // UtcOffset sign inverted, so the stored local value is (UTC + |offset|) rather than the real local time;
    // reconstruct it as utc + (utc - local). Windows/Linux write the field as true local time, so use it directly.
    private static (ushort time, ushort date) EncodeFileMtime(string file)
    {
        if (IsMacOs)
        {
            // The Pocket reads the raw local timestamp exFAT stores. macOS writes it with the UtcOffset sign
            // inverted, so the stored value is (mtime-as-UTC + |offset|) using the offset in effect when the
            // file was WRITTEN, not the mtime's own DST. This path only runs for files pupdate just wrote, so
            // "written" == now: use the current UTC offset. (Unchanged files keep their preserved value.)
            DateTime utc = File.GetLastWriteTimeUtc(file);
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

            return FatEncode(utc + offset.Duration());
        }

        return FatEncode(File.GetLastWriteTime(file));
    }

    // Encodes a local timestamp as a DOS/FAT (date, time) pair, the format the Pocket bakes into the caches
    // (from each source file's on-disk modification time) and compares against the SD directory to decide
    // whether to rebuild.
    private static (ushort time, ushort date) FatEncode(DateTime dt)
    {
        if (dt.Year < 1980)
        {
            dt = new DateTime(1980, 1, 1, 0, 0, 0);
        }

        ushort time = (ushort)(((dt.Hour & 0x1F) << 11) | ((dt.Minute & 0x3F) << 5) | ((dt.Second / 2) & 0x1F));
        ushort date = (ushort)((((dt.Year - 1980) & 0x7F) << 9) | ((dt.Month & 0xF) << 5) | (dt.Day & 0x1F));

        return (time, date);
    }

    // Standard CRC32 (reflected poly 0xEDB88320, init/xorout 0xFFFFFFFF), chainable through `seed` = the
    // previous entry's CRC: crc32(data, crc32(prev)) == crc32(prev + data). The Pocket's corelist hash is
    // exactly this rolled down the /Cores folder names in directory order (seed 0 for the first core).
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint c = i;

            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }

    internal static uint Crc32(byte[] data, uint seed)
    {
        uint crc = seed ^ 0xFFFFFFFF;

        foreach (byte b in data)
        {
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    // ===== unified model =====
    // Gathers one self-consistent snapshot the five caches are all generated from: the platform list (master
    // order = existing platforms_cache order, new platforms appended) and the core list (order = existing
    // corelist order, new cores appended). Reproducible fields (strings, year, FAT mtimes, file sizes, cross-
    // reference indexes) are computed from the live files; the two opaque fields (corelist hash, platforms_cache
    // trailing byte) are preserved from the existing caches when present. Cached for the lifetime of the run.

    private CacheModel model;

    public CacheModel BuildModel()
    {
        if (this.model != null)
        {
            return this.model;
        }

        var cores = this.GatherCores();
        var platforms = this.GatherPlatforms(cores);

        this.model = new CacheModel
        {
            Cores = cores,
            Platforms = platforms,
            PlatformsCacheTrailingByte = this.ReadPlatformsCacheTrailingByte()
        };

        return this.model;
    }

    private List<CoreRow> GatherCores()
    {
        var found = new List<CoreRow>();
        string coresDirectory = Path.Combine(this.installPath, "Cores");

        if (!Directory.Exists(coresDirectory))
        {
            return found;
        }

        var existingMtimes = this.ReadExistingCoreMtimes();
        uint seed = 0;

        // Directory (readdir) order. The Pocket enumerates /Cores in on-disk order and its corelist hash
        // chains through exactly that order, so we must NOT sort - a different order yields different hashes
        // and forces a rebuild. .NET returns the OS/readdir order here (matches the Pocket's exFAT ordering).
        foreach (string directory in Directory.GetDirectories(coresDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string folder = Path.GetFileName(directory);

            // macOS AppleDouble sidecars aren't cores; the Pocket skips them and they aren't in the hash chain
            if (folder.StartsWith("._", StringComparison.Ordinal))
            {
                continue;
            }

            string coreJson = Path.Combine(directory, "core.json");

            if (!File.Exists(coreJson))
            {
                continue;
            }

            var info = this.coresService.ReadCoreJson(folder);

            if (info?.metadata == null)
            {
                continue;
            }

            string[] platformIds = info.metadata.platform_ids ?? Array.Empty<string>();
            string corelistId = (platformIds.Length > 0 ? platformIds[0] : info.metadata.shortname ?? string.Empty)
                .ToLowerInvariant();

            // Rolling CRC32 of the folder name seeded by the running hash (== CRC32 of every folder name
            // concatenated up to and including this one) - the Pocket's freshness hash (verified byte-exact).
            seed = Crc32(CacheEncoding.GetBytes(folder), seed);

            // Keep the Pocket's baked mtime unless pupdate rewrote this core this run; only then recompute from
            // the freshly-written file. Cores the Pocket never cached (new) are recomputed too.
            var key = (info.metadata.author ?? string.Empty, info.metadata.shortname ?? string.Empty);
            (ushort time, ushort date) = !this.recomputedFolders.Contains(folder)
                && existingMtimes.TryGetValue(key, out var baked)
                    ? baked
                    : EncodeFileMtime(coreJson);

            found.Add(new CoreRow
            {
                Folder = folder,
                Author = info.metadata.author,
                Shortname = info.metadata.shortname,
                Version = info.metadata.version,
                PlatformIds = platformIds,
                CorelistId = corelistId,
                FatTime = time,
                FatDate = date,
                JsonSize = (uint)new FileInfo(coreJson).Length,
                HashB = seed
            });
        }

        return found;
    }

    // Reads (author, shortname) per core from cores_cache.bin in file order (96-byte records). This defines
    // the core ordering the grouped views' owning-core index references.
    private List<(string author, string shortname)> ReadExistingCoresCache()
    {
        var keys = new List<(string, string)>();
        string path = Path.Combine(this.SystemDirectory, "cores_cache.bin");

        if (!File.Exists(path))
        {
            return keys;
        }

        byte[] data = File.ReadAllBytes(path);

        for (int offset = 0; offset + 96 <= data.Length; offset += 96)
        {
            keys.Add((ReadField(data, offset, 32), ReadField(data, offset + 32, 32)));
        }

        return keys;
    }

    private static string ReadField(byte[] data, int offset, int length)
    {
        int end = Array.IndexOf(data, (byte)0, offset, length);

        return CacheEncoding.GetString(data, offset, (end < 0 ? offset + length : end) - offset);
    }

    private List<PlatformRow> GatherPlatforms(List<CoreRow> cores)
    {
        var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (CoreRow core in cores)
        {
            foreach (string id in core.PlatformIds)
            {
                installedIds.Add(id);
            }
        }

        // active platforms only (top-level Platforms folder; the Pocket ignores Platforms/_archive)
        var active = new Dictionary<string, PlatformRow>(StringComparer.Ordinal);
        var preservedMtimes = this.ReadExistingPlatformMtimes();
        string platformsDirectory = Path.Combine(this.installPath, "Platforms");

        if (Directory.Exists(platformsDirectory))
        {
            foreach (string file in Directory.GetFiles(platformsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                Platform platform = ReadPlatformFromFile(file);

                if (platform == null)
                {
                    continue;
                }

                // Preserve the mtime the Pocket already baked for this platform (correct regardless of which OS
                // wrote the file); only reconstruct from the file for platforms the Pocket has never cached.
                var (time, date) = preservedMtimes.TryGetValue(id, out var m)
                    ? m
                    : EncodeFileMtime(file);

                active[id] = new PlatformRow
                {
                    Id = id,
                    Category = platform.category,
                    Name = platform.name,
                    Manufacturer = platform.manufacturer,
                    Year = platform.year,
                    FatTime = time,
                    FatDate = date,
                    JsonSize = (uint)new FileInfo(file).Length,
                    Installed = installedIds.Contains(id)
                };
            }
        }

        // Order = alphabetical by display name, case-insensitive - the Pocket's platforms_cache order
        // (verified byte-exact vs its own rebuild), not the historical/scan order.
        return active.Values
            .OrderBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Platform ReadPlatformFromFile(string file)
    {
        try
        {
            var platforms = JsonConvert.DeserializeObject<Dictionary<string, Platform>>(File.ReadAllText(file));

            return platforms != null && platforms.TryGetValue("platform", out Platform platform) ? platform : null;
        }
        catch
        {
            return null;
        }
    }

    private byte ReadPlatformsCacheTrailingByte()
    {
        string path = Path.Combine(this.SystemDirectory, "platforms_cache.bin");

        if (!File.Exists(path))
        {
            return 0;
        }

        byte[] data = File.ReadAllBytes(path);

        return data.Length % 124 == 1 ? data[data.Length - 1] : (byte)0;
    }

    // Maps (author, shortname) -> the FAT mtime the Pocket baked into corelist_cache.bin, by pairing the
    // index-aligned cores_cache keys with the corelist mtimes. Lets us keep the Pocket's timestamps for
    // unchanged cores without reproducing its exFAT timestamp reading.
    private Dictionary<(string, string), (ushort time, ushort date)> ReadExistingCoreMtimes()
    {
        var map = new Dictionary<(string, string), (ushort time, ushort date)>();
        var keys = this.ReadExistingCoresCache();
        var corelist = this.ReadExistingCorelist();

        for (int i = 0; i < keys.Count && i < corelist.Count; i++)
        {
            map[keys[i]] = (corelist[i].time, corelist[i].date);
        }

        return map;
    }

    private List<(string id, uint hash, ushort time, ushort date)> ReadExistingCorelist()
    {
        var list = new List<(string, uint, ushort, ushort)>();
        string path = Path.Combine(this.SystemDirectory, "corelist_cache.bin");

        if (!File.Exists(path))
        {
            return list;
        }

        byte[] data = File.ReadAllBytes(path);

        for (int offset = 0; offset + 80 <= data.Length; offset += 80)
        {
            int end = Array.IndexOf(data, (byte)0, offset + 4, 64);
            string id = CacheEncoding.GetString(data, offset + 4, (end < 0 ? offset + 4 + 64 : end) - (offset + 4));
            ushort time = (ushort)(data[offset + 0x44] | (data[offset + 0x45] << 8));
            ushort date = (ushort)(data[offset + 0x46] | (data[offset + 0x47] << 8));
            uint hash = (uint)(data[offset + 0x4c] | (data[offset + 0x4d] << 8) | (data[offset + 0x4e] << 16) |
                               (data[offset + 0x4f] << 24));
            list.Add((id, hash, time, date));
        }

        return list;
    }

    // Maps platform id -> the FAT mtime the Pocket baked into platforms_cache.bin. Preserving these avoids
    // having to reproduce the Pocket's exFAT timestamp reading (which the host can't do reliably).
    private Dictionary<string, (ushort time, ushort date)> ReadExistingPlatformMtimes()
    {
        var map = new Dictionary<string, (ushort, ushort)>(StringComparer.Ordinal);
        string path = Path.Combine(this.SystemDirectory, "platforms_cache.bin");

        if (!File.Exists(path))
        {
            return map;
        }

        byte[] data = File.ReadAllBytes(path);

        for (int offset = 0; offset + 124 <= data.Length; offset += 124)
        {
            int end = Array.IndexOf(data, (byte)0, offset, IdFieldSize);
            string id = CacheEncoding.GetString(data, offset, (end < 0 ? offset + IdFieldSize : end) - offset);
            ushort time = (ushort)(data[offset + 0x74] | (data[offset + 0x75] << 8));
            ushort date = (ushort)(data[offset + 0x76] | (data[offset + 0x77] << 8));
            map[id] = (time, date);
        }

        return map;
    }

    public class CacheModel
    {
        public List<CoreRow> Cores { get; set; }
        public List<PlatformRow> Platforms { get; set; }
        public byte PlatformsCacheTrailingByte { get; set; }

        // Indexes (in core order) of the cores that own a platform (its id appears in their platform_ids).
        public List<int> OwningCoreIndexes(string platformId)
        {
            var result = new List<int>();

            for (int i = 0; i < this.Cores.Count; i++)
            {
                if (this.Cores[i].PlatformIds.Contains(platformId, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(i);
                }
            }

            return result;
        }
    }

    public class PlatformRow
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public int Year { get; set; }
        public ushort FatTime { get; set; }
        public ushort FatDate { get; set; }
        public uint JsonSize { get; set; }
        public bool Installed { get; set; }
    }

    public class CoreRow
    {
        public string Folder { get; set; }
        public string Author { get; set; }
        public string Shortname { get; set; }
        public string Version { get; set; }
        public string[] PlatformIds { get; set; }
        public string CorelistId { get; set; }
        public ushort FatTime { get; set; }
        public ushort FatDate { get; set; }
        public uint JsonSize { get; set; }
        public uint HashB { get; set; }
    }

    // Generates the full, self-consistent set of cache files (complete views).
    public List<(string name, byte[] bytes)> BuildAll()
    {
        return new List<(string, byte[])>
        {
            ("platforms_cache.bin", this.BuildPlatformsCache()),
            ("cores_cache.bin", this.BuildCoresCache()),
            ("corelist_cache.bin", this.BuildCorelistCache()),
            ("platform_viewby_category.bin", this.BuildPlatformViewByCategory()),
            ("core_viewby_platform.bin", this.BuildCoreViewByPlatform())
        };
    }

    // Writes the full consistent cache set into /System (backing up existing files to .pupdate.bak). Existing
    // entries keep the Pocket's baked mtimes; files updated this run get recomputed mtimes. Writes the complete
    // views, so the caller must first confirm ProjectedCoreViewSize is under BufferSize - an over-limit set
    // can't be pre-built around (the Pocket rebuilds the view on boot and crashes regardless).
    public List<(string name, int bytes, bool backedUp)> ApplyToSystem()
    {
        Directory.CreateDirectory(this.SystemDirectory);

        var results = new List<(string, int, bool)>();

        foreach (var (name, bytes) in this.BuildAll())
        {
            string target = Path.Combine(this.SystemDirectory, name);
            string backup = target + ".pupdate.bak";
            bool backedUp = false;

            if (File.Exists(target) && !File.Exists(backup))
            {
                File.Copy(target, backup);
                backedUp = true;
            }

            File.WriteAllBytes(target, bytes);
            results.Add((name, bytes.Length, backedUp));
        }

        return results;
    }

    // Byte-compares a generated cache against the one currently in /System.
    public VerifyResult Verify(string name, byte[] generated)
    {
        var result = new VerifyResult { Name = name, Generated = generated };
        string path = Path.Combine(this.SystemDirectory, name);

        if (!File.Exists(path))
        {
            return result;
        }

        byte[] existing = File.ReadAllBytes(path);
        result.ExistingFound = true;
        result.ExistingLength = existing.Length;

        int min = Math.Min(existing.Length, generated.Length);
        int firstDiff = -1;

        for (int i = 0; i < min; i++)
        {
            if (existing[i] != generated[i])
            {
                firstDiff = i;
                break;
            }
        }

        if (firstDiff == -1 && existing.Length != generated.Length)
        {
            firstDiff = min;
        }

        result.Match = firstDiff == -1;
        result.FirstDifferenceOffset = firstDiff;

        return result;
    }

    // Lightweight description of an existing cache file for the analysis report.
    public List<CacheFileInfo> AnalyzeExisting()
    {
        var files = new List<CacheFileInfo>();

        void Add(string name, int? recordSize, bool isView)
        {
            string path = Path.Combine(this.SystemDirectory, name);
            var info = new CacheFileInfo { Name = name, Exists = File.Exists(path), IsView = isView };

            if (info.Exists)
            {
                info.Size = new FileInfo(path).Length;

                if (isView)
                {
                    info.Overflow = info.Size > BufferSize;
                }

                if (recordSize is > 0)
                {
                    long body = isView ? info.Size - HeaderSize : info.Size;

                    if (body > 0)
                    {
                        info.RecordCount = (int)(body / recordSize.Value);
                    }
                }
            }

            files.Add(info);
        }

        Add("platforms_cache.bin", 124, false);
        Add("platform_viewby_category.bin", ViewRecordSize, true);
        Add("core_viewby_platform.bin", null, true);
        Add("cores_cache.bin", 96, false);
        Add("corelist_cache.bin", 80, false);

        return files;
    }

    public class VerifyResult
    {
        public string Name { get; set; }
        public bool ExistingFound { get; set; }
        public byte[] Generated { get; set; }
        public int ExistingLength { get; set; }
        public bool Match { get; set; }
        public int FirstDifferenceOffset { get; set; }
    }

    public class CacheFileInfo
    {
        public string Name { get; set; }
        public bool Exists { get; set; }
        public long Size { get; set; }
        public int? RecordCount { get; set; }
        public bool IsView { get; set; }
        public bool Overflow { get; set; }
    }
}
