using FluentAssertions;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class SettingsServicePluginTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public SettingsServicePluginTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private string MakeScratchDir() => Directory.CreateDirectory(
        Path.Combine(_temp.Path, "settings-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void SetPluginAutoRun_PersistsAcrossReload()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);

        svc.SetPluginAutoRun("owner.repo", "owner/repo", true, PluginPhase.before);
        svc.Save();

        var reloaded = new SettingsService(dir);
        var settings = reloaded.GetPluginSettings("owner.repo");

        settings.run_on_update.Should().BeTrue();
        settings.phase.Should().Be(PluginPhase.before);
        settings.repo.Should().Be("owner/repo");
        reloaded.PluginSettings.Should().ContainKey("OWNER.REPO", "lookups are case-insensitive");
    }

    [Fact]
    public void SetPluginAutoRun_Disabled_RemovesEntry()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);
        svc.SetPluginAutoRun("owner.repo", "owner/repo", true);

        svc.SetPluginAutoRun("owner.repo", null, false);
        svc.Save();

        svc.PluginSettings.Should().BeEmpty();
        File.ReadAllText(Path.Combine(dir, "pupdate_settings.json")).Should().NotContain("owner.repo");
    }

    [Fact]
    public void GetPluginSettings_Unknown_ReturnsDefaults_WithoutMutating()
    {
        string dir = MakeScratchDir();
        var svc = new SettingsService(dir);

        var settings = svc.GetPluginSettings("unknown.plugin");

        settings.run_on_update.Should().BeFalse();
        settings.phase.Should().Be(PluginPhase.after);
        svc.GetPluginSettings(null).run_on_update.Should().BeFalse();

        svc.Save();
        File.ReadAllText(Path.Combine(dir, "pupdate_settings.json")).Should().NotContain("unknown.plugin");
    }

    [Fact]
    public void Constructor_FileWithoutPluginSettings_StillHasDictionary()
    {
        string dir = MakeScratchDir();
        File.WriteAllText(Path.Combine(dir, "pupdate_settings.json"),
            """{ "config": { "download_assets": false }, "plugin_settings": null }""");

        var svc = new SettingsService(dir);

        svc.PluginSettings.Should().NotBeNull().And.BeEmpty();
    }
}
