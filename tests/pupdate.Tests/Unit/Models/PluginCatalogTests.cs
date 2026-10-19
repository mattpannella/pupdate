using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;

namespace Pannella.Tests.Unit.Models;

public class PluginCatalogTests
{
    private const string Sample =
        """
        {
          "plugins": [
            {
              "id": "coinop-license",
              "repo": "https://github.com/mattpannella/pocket-plugin-coinop-license",
              "name": "Coin-Op Collection License",
              "author": "mattpannella",
              "description": "Fetches your Coin-Op Collection license key.",
              "tags": ["license"],
              "requires_input": false,
              "phase": "before",
              "links": ["https://coinopcollection.org"]
            },
            {
              "id": "example",
              "repo": "openfpga-library/pocket-plugin",
              "name": "Example"
            }
          ]
        }
        """;

    [Fact]
    public void Parse_ReadsEntries()
    {
        var catalog = JsonConvert.DeserializeObject<PluginCatalog>(Sample);

        catalog!.plugins.Should().HaveCount(2);

        var first = catalog.plugins[0];
        first.id.Should().Be("coinop-license");
        first.Slug.Should().Be("mattpannella/pocket-plugin-coinop-license");
        first.DirectoryName.Should().Be("mattpannella.pocket-plugin-coinop-license");
        first.RepoUrl.Should().Be("https://github.com/mattpannella/pocket-plugin-coinop-license");
        first.requires_input.Should().BeFalse();
        first.phase.Should().Be(PluginPhase.before);
        first.tags.Should().ContainSingle("license");
        first.links.Should().ContainSingle("https://coinopcollection.org");
    }

    [Fact]
    public void Parse_MinimalEntry_HasSafeDefaults()
    {
        var catalog = JsonConvert.DeserializeObject<PluginCatalog>(Sample);
        var minimal = catalog!.plugins[1];

        minimal.Slug.Should().Be("openfpga-library/pocket-plugin");
        minimal.RepoUrl.Should().Be("https://github.com/openfpga-library/pocket-plugin");
        minimal.phase.Should().BeNull();
        minimal.requires_input.Should().BeFalse();
        minimal.tags.Should().BeEmpty();
        minimal.links.Should().BeEmpty();
    }

    [Fact]
    public void Slug_IsNull_WhenRepoDoesNotParse()
    {
        var entry = new PluginCatalogEntry { repo = "not a repo" };

        entry.Slug.Should().BeNull();
        entry.DirectoryName.Should().BeNull();
        entry.RepoUrl.Should().Be("not a repo");
    }
}
