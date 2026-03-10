// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using Pannella.Models.OpenFPGA_Cores_Inventory.V2;
using CoreV2 = Pannella.Models.OpenFPGA_Cores_Inventory.V2.Core;
using CoreV3 = Pannella.Models.OpenFPGA_Cores_Inventory.V3.Core;
using PlatformV2 = Pannella.Models.OpenFPGA_Cores_Inventory.V2.Platform;
using PlatformV3 = Pannella.Models.OpenFPGA_Cores_Inventory.V3.Platform;
using RepositoryV2 = Pannella.Models.OpenFPGA_Cores_Inventory.V2.Repository;
using RepositoryV3 = Pannella.Models.OpenFPGA_Cores_Inventory.V3.Repository;

namespace Pannella.Models.OpenFPGA_Cores_Inventory.V3;

public static class CoreMapper
{
    public static CoreV2 MapToCore(
        CoreV3 core,
        IReadOnlyDictionary<string, PlatformV3> platformsById)
    {
        if (core?.releases == null || core.releases.Count == 0)
            return null;

        Release release = GetLatestRelease(core.releases);

        if (release?.core?.metadata == null)
            return null;

        ReleaseMetadata meta = release.core.metadata;
        string platformId = meta.platform_ids is { Count: > 0 }
            ? meta.platform_ids[0]
            : null;

        if (string.IsNullOrEmpty(platformId))
            return null;

        var mapped = new CoreV2
        {
            identifier = core.id,
            repository = MapRepository(core.repository),
            platform = ResolvePlatform(platformId, platformsById, core.id),
            platform_id = platformId,
            sponsor = MapFundingToSponsor(core.repository?.funding),
            download_url = release.download_url,
            release_date = meta.date_release,
            version = meta.version,
            requires_license = release.requires_license,
            updaters = release.updaters
        };

        return mapped;
    }

    public static Release GetLatestRelease(List<Release> releases)
    {
        if (releases == null || releases.Count == 0)
            return null;

        if (releases.Count == 1)
            return releases[0];

        Release latest = releases[0];
        string latestDate = latest.core?.metadata?.date_release;

        for (int i = 1; i < releases.Count; i++)
        {
            string d = releases[i].core?.metadata?.date_release;
            if (string.IsNullOrEmpty(d))
                continue;

            if (string.IsNullOrEmpty(latestDate) || string.CompareOrdinal(d, latestDate) > 0)
            {
                latest = releases[i];
                latestDate = d;
            }
        }

        return latest;
    }

    private static RepositoryV2 MapRepository(RepositoryV3 repo)
    {
        if (repo == null)
            return null;

        return new RepositoryV2
        {
            platform = repo.platform,
            owner = repo.owner,
            name = repo.name
        };
    }

    private static Sponsor MapFundingToSponsor(Funding funding)
    {
        if (funding == null)
            return null;

        return new Sponsor
        {
            github = funding.github,
            patreon = funding.patreon,
            custom = funding.custom
        };
    }

    private static PlatformV2 ResolvePlatform(
        string platformId,
        IReadOnlyDictionary<string, PlatformV3> platformsById,
        string coreId)
    {
        if (platformsById == null)
        {
            throw new InvalidOperationException(
                "Platforms catalog was not loaded. Cannot map core to v2 without platforms.json.");
        }

        if (!platformsById.TryGetValue(platformId, out PlatformV3 platformV3))
        {
            throw new InvalidOperationException(
                $"Core '{coreId}' references platform_id '{platformId}' which is not in the platforms catalog. " +
                "Ensure platforms.json is loaded and contains this platform.");
        }

        return new PlatformV2
        {
            category = platformV3.category,
            name = platformV3.name,
            manufacturer = platformV3.manufacturer,
            year = platformV3.year
        };
    }
}
