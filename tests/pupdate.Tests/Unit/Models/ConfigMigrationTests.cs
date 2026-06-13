using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Models.Settings;

namespace Pannella.Tests.Unit.Models;

public class ConfigMigrationTests
{
    [Fact]
    public void Migrate_LegacyArchiveName_PopulatesDefaultArchive()
    {
        var json = "{ \"archive_name\": \"my-custom-files\" }";
        var config = JsonConvert.DeserializeObject<Config>(json);

        config.Migrate();

        var defaultArchive = config.archives.FirstOrDefault(a => a.name == "default");
        defaultArchive.Should().NotBeNull();
        defaultArchive!.archive_name.Should().Be("my-custom-files");
    }

    [Fact]
    public void Migrate_LegacyGnwArchiveName_PopulatesGameAndWatchArchive()
    {
        var json = "{ \"gnw_archive_name\": \"alt-gnw\" }";
        var config = JsonConvert.DeserializeObject<Config>(json);

        config.Migrate();

        var gnw = config.archives.FirstOrDefault(a => a.name == "agg23.GameAndWatch");
        gnw.Should().NotBeNull();
        gnw!.archive_name.Should().Be("alt-gnw");
    }

    [Fact]
    public void Migrate_LegacyDownloadGnwRoms_SetsEnabledFlag()
    {
        var json = "{ \"download_gnw_roms\": true }";
        var config = JsonConvert.DeserializeObject<Config>(json);

        config.Migrate();

        var gnw = config.archives.FirstOrDefault(a => a.name == "agg23.GameAndWatch");
        gnw!.enabled.Should().BeTrue();
    }

    [Fact]
    public void Migrate_LegacyCustomArchive_PopulatesCustomEntry()
    {
        var json = """{ "custom_archive": { "url": "https://example.com", "index": "files.json" } }""";
        var config = JsonConvert.DeserializeObject<Config>(json);

        config.Migrate();

        var custom = config.archives.FirstOrDefault(a => a.name == "custom");
        custom!.url.Should().Be("https://example.com");
        custom.index.Should().Be("files.json");
        custom.archive_name.Should().Be("custom",
            "the bugfix branch ensures custom archives have archive_name populated");
    }

    [Fact]
    public void Migrate_NoLegacyFields_LeavesDefaultsIntact()
    {
        var config = new Config();
        config.Migrate();

        config.archives.Should().Contain(a => a.name == "default");
        config.archives.Should().Contain(a => a.name == "custom");
        config.archives.Should().Contain(a => a.name == "agg23.GameAndWatch");
    }
}
