using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Models.Settings;

namespace Pannella.Tests.Unit.Models;

public class ConfigMigrationTests
{
    [Fact]
    public void AiFilterDefaults_OffAndDisabledThreshold()
    {
        var config = new Config();

        config.filter_ai_cores.Should().BeFalse();
        // 100 = effectively off: nothing scores above 1.0, so nothing is tagged or filtered by default.
        config.ai_core_threshold.Should().Be(100);
    }

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

    // ---------- settings retired in favour of plugins ----------

    private static Config Load(string json)
    {
        var config = JsonConvert.DeserializeObject<Config>(json);
        config!.Migrate();
        return config;
    }

    [Theory]
    [InlineData("""{ "coin_op_beta": true }""", RetiredSettingMigrations.COINOP_LICENSE)]
    [InlineData("""{ "fix_jt_names": true }""", RetiredSettingMigrations.JOTEGO_PLATFORM_NAMES)]
    [InlineData("""{ "jt_beta_github_fetch": true }""", RetiredSettingMigrations.JOTEGO_BETA_KEY)]
    [InlineData("""{ "jt_beta_patreon_fetch": true }""", RetiredSettingMigrations.JOTEGO_BETA_KEY)]
    [InlineData("""{ "patreon_session_cookie": "abc" }""", RetiredSettingMigrations.JOTEGO_BETA_KEY)]
    public void Migrate_RetiredSettingOn_QueuesReplacementPlugin(string json, string pluginId)
    {
        var config = Load(json);

        config.pending_plugin_migrations.Should().Equal(pluginId);
        RetiredSettingMigrations.Find(pluginId).Should().NotBeNull();
    }

    [Theory]
    [InlineData("""{ "coin_op_beta": false, "fix_jt_names": false, "jt_beta_github_fetch": false, "jt_beta_patreon_fetch": false, "patreon_session_cookie": null }""")]
    [InlineData("""{ "download_assets": true }""")]
    public void Migrate_RetiredSettingsOffOrAbsent_QueuesNothing(string json)
    {
        var config = Load(json);

        config.pending_plugin_migrations.Should().BeNull();
        JsonConvert.SerializeObject(config).Should().NotContain("pending_plugin_migrations");
    }

    [Fact]
    public void Migrate_RetiredKeys_AreDroppedOnSerialize()
    {
        var config = Load("""{ "coin_op_beta": true, "fix_jt_names": true, "jt_beta_github_fetch": true, "jt_beta_patreon_fetch": true, "patreon_session_cookie": "abc" }""");

        string json = JsonConvert.SerializeObject(config);

        json.Should().NotContain("coin_op_beta").And.NotContain("fix_jt_names")
            .And.NotContain("jt_beta_github_fetch").And.NotContain("jt_beta_patreon_fetch")
            .And.NotContain("patreon_session_cookie").And.NotContain("abc");
        json.Should().Contain("pending_plugin_migrations");
        config.pending_plugin_migrations.Should().BeEquivalentTo(new[]
        {
            RetiredSettingMigrations.COINOP_LICENSE,
            RetiredSettingMigrations.JOTEGO_PLATFORM_NAMES,
            RetiredSettingMigrations.JOTEGO_BETA_KEY
        });
    }

    [Fact]
    public void Migrate_AlreadyPending_DoesNotDuplicate()
    {
        var config = Load("""{ "coin_op_beta": true, "pending_plugin_migrations": ["coinop-license"] }""");

        config.pending_plugin_migrations.Should().ContainSingle();
    }

    [Fact]
    public void ClearPendingPluginMigration_RemovesEntry_AndNullsEmptyList()
    {
        var config = Load("""{ "coin_op_beta": true, "jt_beta_github_fetch": true }""");

        config.ClearPendingPluginMigration("COINOP-LICENSE");
        config.pending_plugin_migrations.Should().Equal(RetiredSettingMigrations.JOTEGO_BETA_KEY);

        config.ClearPendingPluginMigration(RetiredSettingMigrations.JOTEGO_BETA_KEY);
        config.pending_plugin_migrations.Should().BeNull();
    }
}
