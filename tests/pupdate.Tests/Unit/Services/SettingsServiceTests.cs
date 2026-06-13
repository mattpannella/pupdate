using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class SettingsServiceTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public SettingsServiceTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private string MakeScratchDir() => Directory.CreateDirectory(
        Path.Combine(_temp.Path, "settings-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void Constructor_NoExistingFile_AppliesDefaultsAndCreatesFile()
    {
        string dir = MakeScratchDir();

        var svc = new SettingsService(dir);

        File.Exists(Path.Combine(dir, "pupdate_settings.json")).Should().BeTrue();
        svc.Config.download_assets.Should().BeTrue("default in Config");
        svc.Config.download_firmware.Should().BeTrue();
        svc.Config.crc_check.Should().BeTrue();
    }

    [Fact]
    public void EnableCore_AddsNewCore_WithSkipFalse()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);

        svc.EnableCore("agg23.NES");

        svc.GetCoreSettings("agg23.NES").skip.Should().BeFalse();
    }

    [Fact]
    public void DisableCore_AddsNewCore_WithSkipTrue()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);

        svc.DisableCore("agg23.NES");

        svc.GetCoreSettings("agg23.NES").skip.Should().BeTrue();
    }

    [Fact]
    public void DisableCore_FlipsExistingCoreToSkipped()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);
        svc.EnableCore("agg23.NES");

        svc.DisableCore("agg23.NES");

        svc.GetCoreSettings("agg23.NES").skip.Should().BeTrue();
    }

    [Fact]
    public void PinCoreVersion_PersistsAcrossReload()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);
        svc.EnableCore("agg23.NES");

        svc.PinCoreVersion("agg23.NES", "v2.0.0");
        svc.Save();

        var reloaded = new SettingsService(dir);
        reloaded.GetCoreSettings("agg23.NES").pinned_version.Should().Be("v2.0.0");
    }

    [Fact]
    public void UnpinCoreVersion_ClearsPinnedVersion()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);
        svc.EnableCore("agg23.NES");
        svc.PinCoreVersion("agg23.NES", "v1.0.0");

        svc.UnpinCoreVersion("agg23.NES");

        svc.GetCoreSettings("agg23.NES").pinned_version.Should().BeNull();
    }

    [Fact]
    public void GetCoreSettings_UnknownCore_ReturnsDefaults_WithoutMutatingDictionary()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);

        var s = svc.GetCoreSettings("unknown.core");

        s.skip.Should().BeFalse();
        s.download_assets.Should().BeTrue();
        s.platform_rename.Should().BeTrue();

        svc.Save();
        string json = File.ReadAllText(Path.Combine(dir, "pupdate_settings.json"));
        json.Should().NotContain("unknown.core",
            "GetCoreSettings on an unknown core must NOT add it to core_settings");
    }

    [Fact]
    public void Constructor_LegacyFile_MigratesToNewFile()
    {
        string dir = MakeScratchDir();
        var legacy = new
        {
            config = new { download_assets = false, github_token = "abc" },
            core_settings = new Dictionary<string, object>
            {
                ["agg23.NES"] = new { skip = true }
            }
        };
        File.WriteAllText(
            Path.Combine(dir, "pocket_updater_settings.json"),
            JsonConvert.SerializeObject(legacy));

        var svc = new SettingsService(dir);

        File.Exists(Path.Combine(dir, "pocket_updater_settings.json")).Should().BeFalse(
            "legacy file should be deleted after migration");
        File.Exists(Path.Combine(dir, "pupdate_settings.json")).Should().BeTrue();
        svc.Config.download_assets.Should().BeFalse();
        svc.Config.github_token.Should().Be("abc");
        svc.GetCoreSettings("agg23.NES").skip.Should().BeTrue();
    }

    [Fact]
    public void Constructor_BothLegacyAndModernPresent_ModernWins()
    {
        string dir = MakeScratchDir();
        File.WriteAllText(
            Path.Combine(dir, "pocket_updater_settings.json"),
            JsonConvert.SerializeObject(new { config = new { github_token = "legacy" } }));
        File.WriteAllText(
            Path.Combine(dir, "pupdate_settings.json"),
            JsonConvert.SerializeObject(new { config = new { github_token = "modern" } }));

        var svc = new SettingsService(dir);

        svc.Config.github_token.Should().Be("modern");
    }
}
