using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.Settings;

namespace Pannella.Tests.Unit.Models;

public class PluginSettingsRoundTripTests
{
    private static string Serialize(object value) =>
        JsonConvert.SerializeObject(value, Formatting.None,
            new JsonSerializerSettings { ContractResolver = ArchiveContractResolver.INSTANCE });

    [Fact]
    public void PhaseBefore_IsWrittenAsString()
    {
        var s = new PluginSettings { repo = "owner/repo", run_on_update = true, phase = PluginPhase.before };

        string json = Serialize(s);

        json.Should().Contain("\"phase\":\"before\"");
        json.Should().Contain("\"repo\":\"owner/repo\"");
        json.Should().Contain("\"run_on_update\":true");
    }

    [Fact]
    public void PhaseAfter_IsOmitted()
    {
        var s = new PluginSettings { run_on_update = true };

        Serialize(s).Should().NotContain("phase");
    }

    [Fact]
    public void RoundTrip_PreservesFields()
    {
        var settings = new Settings();
        settings.plugin_settings["owner.repo"] = new PluginSettings
        {
            repo = "owner/repo",
            run_on_update = true,
            phase = PluginPhase.before
        };

        var restored = JsonConvert.DeserializeObject<Settings>(Serialize(settings));

        restored.plugin_settings.Should().ContainKey("owner.repo");
        restored.plugin_settings["owner.repo"].repo.Should().Be("owner/repo");
        restored.plugin_settings["owner.repo"].run_on_update.Should().BeTrue();
        restored.plugin_settings["owner.repo"].phase.Should().Be(PluginPhase.before);
    }

    [Fact]
    public void Deserialize_KeyLookupIsCaseInsensitive()
    {
        var restored = JsonConvert.DeserializeObject<Settings>(
            """{ "plugin_settings": { "Owner.Repo": { "run_on_update": true } } }""");

        restored.plugin_settings.TryGetValue("owner.repo", out var value).Should().BeTrue();
        value!.run_on_update.Should().BeTrue();
        value.phase.Should().Be(PluginPhase.after, "phase defaults to after when omitted");
    }

    [Fact]
    public void Deserialize_LegacyFileWithoutPluginSettings_HasEmptyDictionary()
    {
        var restored = JsonConvert.DeserializeObject<Settings>("""{ "config": { "download_assets": false } }""");

        restored.plugin_settings.Should().NotBeNull().And.BeEmpty();
    }
}
