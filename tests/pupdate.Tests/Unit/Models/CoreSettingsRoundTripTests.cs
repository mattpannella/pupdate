using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models.Settings;

namespace Pannella.Tests.Unit.Models;

public class CoreSettingsRoundTripTests
{
    private static string Serialize(CoreSettings s) =>
        JsonConvert.SerializeObject(s, Formatting.None,
            new JsonSerializerSettings { ContractResolver = ArchiveContractResolver.INSTANCE });

    [Fact]
    public void PinnedVersionNull_IsOmittedFromJson()
    {
        var s = new CoreSettings { pinned_version = null };

        Serialize(s).Should().NotContain("pinned_version");
    }

    [Fact]
    public void PinnedVersionSet_IsIncluded()
    {
        var s = new CoreSettings { pinned_version = "v1.2.3" };

        Serialize(s).Should().Contain("\"pinned_version\":\"v1.2.3\"");
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new CoreSettings
        {
            skip = true,
            download_assets = false,
            platform_rename = false,
            pocket_extras = true,
            pocket_extras_version = "1.0",
            display_modes = true,
            selected_display_modes = "0x10,0x11",
            requires_license = true,
            pinned_version = "v3.0.0"
        };

        var json = Serialize(original);
        var restored = JsonConvert.DeserializeObject<CoreSettings>(json);

        restored.skip.Should().BeTrue();
        restored.download_assets.Should().BeFalse();
        restored.platform_rename.Should().BeFalse();
        restored.pocket_extras.Should().BeTrue();
        restored.pocket_extras_version.Should().Be("1.0");
        restored.display_modes.Should().BeTrue();
        restored.selected_display_modes.Should().Be("0x10,0x11");
        restored.requires_license.Should().BeTrue();
        restored.pinned_version.Should().Be("v3.0.0");
    }
}
