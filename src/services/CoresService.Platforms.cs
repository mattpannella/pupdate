using Newtonsoft.Json;
using Pannella.Models;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella.Services;

public partial class CoresService
{
    public const int PLATFORM_LIMIT = 249;

    private string PlatformsDirectory => Path.Combine(this.installPath, "Platforms");

    private string ArchivedPlatformsDirectory => Path.Combine(this.PlatformsDirectory, "_archive");

    // The Analogue Pocket OS only reads platform JSON files from the top level of the
    // "Platforms" folder and ignores subdirectories. Archiving a platform moves its JSON
    // into "Platforms/_archive" so the Pocket ignores it (and it stops counting toward the
    // 249 platform limit) while pupdate can still find it.

    private string GetActivePlatformFilePath(string platformId) =>
        Path.Combine(this.PlatformsDirectory, platformId + ".json");

    private string GetArchivedPlatformFilePath(string platformId) =>
        Path.Combine(this.ArchivedPlatformsDirectory, platformId + ".json");

    // Returns where the platform JSON currently lives: the active path if it exists,
    // otherwise the archived path if it exists, otherwise the active path (the default
    // target for a brand-new file).
    public string GetPlatformFilePath(string platformId)
    {
        string active = this.GetActivePlatformFilePath(platformId);

        if (File.Exists(active))
        {
            return active;
        }

        string archived = this.GetArchivedPlatformFilePath(platformId);

        return File.Exists(archived) ? archived : active;
    }

    public bool IsPlatformArchived(string platformId)
    {
        return File.Exists(this.GetArchivedPlatformFilePath(platformId)) &&
               !File.Exists(this.GetActivePlatformFilePath(platformId));
    }

    // Snapshot of the platform ids that are currently archived. Capture this before an
    // install that copies a core's files into place, then pass it to ReArchivePlatforms
    // afterward so platforms the user archived don't silently reappear in the top-level
    // Platforms folder (which would un-archive them and count against the 249 limit).
    public List<string> GetArchivedPlatformIds()
    {
        if (!Directory.Exists(this.ArchivedPlatformsDirectory))
        {
            return new List<string>();
        }

        return Directory.GetFiles(this.ArchivedPlatformsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();
    }

    // For each previously-archived platform whose JSON was re-created in the top-level
    // Platforms folder by an install, move it back into Platforms/_archive (keeping the
    // platform archived and refreshing the archived copy with the newest version).
    public void ReArchivePlatforms(IEnumerable<string> previouslyArchived)
    {
        foreach (string platformId in previouslyArchived)
        {
            string active = this.GetActivePlatformFilePath(platformId);

            if (!File.Exists(active))
            {
                continue;
            }

            string archived = this.GetArchivedPlatformFilePath(platformId);

            Directory.CreateDirectory(Path.GetDirectoryName(archived));
            File.Copy(active, archived, true);
            File.Delete(active);
        }
    }

    // Builds the set of platform ids referenced by an installed core by scanning the local
    // Cores directory and reading each core.json. This works offline and covers local/manual
    // cores that aren't in the openFPGA inventory (and avoids pulling the whole catalog).
    private HashSet<string> GetInstalledPlatformIds()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string coresDirectory = Path.Combine(this.installPath, "Cores");

        if (!Directory.Exists(coresDirectory))
        {
            return ids;
        }

        foreach (string directory in Directory.GetDirectories(coresDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            AnalogueCore info;

            try
            {
                info = this.ReadCoreJson(Path.GetFileName(directory));
            }
            catch
            {
                // a single corrupt core.json shouldn't take down the whole platforms feature
                continue;
            }

            if (info?.metadata?.platform_ids == null)
            {
                continue;
            }

            foreach (string platformId in info.metadata.platform_ids)
            {
                if (!string.IsNullOrEmpty(platformId))
                {
                    ids.Add(platformId);
                }
            }
        }

        return ids;
    }

    private static string ReadPlatformName(string file)
    {
        try
        {
            var platforms = JsonConvert.DeserializeObject<Dictionary<string, Platform>>(File.ReadAllText(file));

            return platforms != null && platforms.TryGetValue("platform", out Platform platform)
                ? platform.name
                : null;
        }
        catch
        {
            return null;
        }
    }

    public List<PlatformInfo> GetPlatforms()
    {
        HashSet<string> installedPlatformIds = this.GetInstalledPlatformIds();
        Dictionary<string, PlatformInfo> platforms = new Dictionary<string, PlatformInfo>(StringComparer.OrdinalIgnoreCase);

        void Collect(string directory, bool archived)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileNameWithoutExtension(file);

                // active files are collected first, so an active entry wins if both exist
                if (platforms.ContainsKey(id))
                {
                    continue;
                }

                platforms[id] = new PlatformInfo
                {
                    Id = id,
                    Name = ReadPlatformName(file) ?? id,
                    Archived = archived,
                    HasInstalledCore = installedPlatformIds.Contains(id)
                };
            }
        }

        Collect(this.PlatformsDirectory, false);
        Collect(this.ArchivedPlatformsDirectory, true);

        return platforms.Values.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void ArchivePlatform(string platformId)
    {
        string active = this.GetActivePlatformFilePath(platformId);

        if (!File.Exists(active))
        {
            WriteMessage($"Platform '{platformId}' is not active. Skipping.");

            return;
        }

        string archived = this.GetArchivedPlatformFilePath(platformId);

        Directory.CreateDirectory(Path.GetDirectoryName(archived));
        File.Copy(active, archived, true);
        File.Delete(active);

        WriteMessage($"Archived platform '{platformId}'.");
    }

    public void UnarchivePlatform(string platformId)
    {
        string archived = this.GetArchivedPlatformFilePath(platformId);

        if (!File.Exists(archived))
        {
            WriteMessage($"Platform '{platformId}' is not archived. Skipping.");

            return;
        }

        string active = this.GetActivePlatformFilePath(platformId);

        Directory.CreateDirectory(Path.GetDirectoryName(active));
        File.Copy(archived, active, true);
        File.Delete(archived);

        WriteMessage($"Unarchived platform '{platformId}'.");
    }

    public int ArchiveUnusedPlatforms()
    {
        var unused = this.GetPlatforms()
            .Where(p => !p.Archived && !p.HasInstalledCore)
            .ToList();

        foreach (PlatformInfo platform in unused)
        {
            this.ArchivePlatform(platform.Id);
        }

        return unused.Count;
    }
}
