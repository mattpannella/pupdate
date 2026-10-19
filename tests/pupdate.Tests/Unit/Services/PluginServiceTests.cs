using FluentAssertions;
using Pannella.Models.Plugins;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class PluginServiceTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public PluginServiceTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    [Theory]
    [InlineData("owner/repo", "owner", "repo")]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    [InlineData("http://github.com/owner/repo.git", "owner", "repo")]
    [InlineData("https://github.com/owner/repo/", "owner", "repo")]
    [InlineData("  owner/my-plugin.v2  ", "owner", "my-plugin.v2")]
    public void TryParseRepoSpec_ParsesSlugsAndUrls(string spec, string owner, string repo)
    {
        PluginService.TryParseRepoSpec(spec, out string o, out string r).Should().BeTrue();
        o.Should().Be(owner);
        r.Should().Be(repo);
        PluginService.NormalizeRepoSlug(spec).Should().Be($"{owner}/{repo}");
        PluginService.DirectoryNameFor(spec).Should().Be($"{owner}.{repo}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just-a-name")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("owner/repo/extra")]
    public void TryParseRepoSpec_RejectsInvalid(string spec)
    {
        PluginService.TryParseRepoSpec(spec, out _, out _).Should().BeFalse();
        PluginService.NormalizeRepoSlug(spec).Should().BeNull();
        PluginService.DirectoryNameFor(spec).Should().BeNull();
    }

    private (string pluginsDir, PluginService svc) NewSvc()
    {
        string root = Path.Combine(_temp.Path, "plugins-" + Guid.NewGuid().ToString("N"));
        string pluginsDir = Path.Combine(root, "plugins");
        Directory.CreateDirectory(pluginsDir);
        var svc = new PluginService(pluginsDir, Path.Combine(root, "pocket"), Path.Combine(root, "data"));
        return (pluginsDir, svc);
    }

    private static void WritePlugin(string pluginsDir, string dirName, string name, string repo = null, string tag = null)
    {
        string dir = Path.Combine(pluginsDir, dirName);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "plugin.wasm"), new byte[] { 0, 0x61, 0x73, 0x6d });
        File.WriteAllText(Path.Combine(dir, "plugin.json"), $$"""{ "name": "{{name}}", "allowed_hosts": [] }""");

        if (repo != null)
        {
            File.WriteAllText(Path.Combine(dir, "installed.json"),
                $$"""{ "repo": "{{repo}}", "release_tag": "{{tag ?? "v1"}}", "installed_at": "2026-01-01T00:00:00Z" }""");
        }
    }

    [Fact]
    public void Discover_ReadsManifestAndInstallInfo_SortedByName()
    {
        var (pluginsDir, svc) = NewSvc();
        WritePlugin(pluginsDir, "zeta.plugin", "Zeta", "zeta/plugin", "v3");
        WritePlugin(pluginsDir, "alpha.plugin", "Alpha");
        Directory.CreateDirectory(Path.Combine(pluginsDir, "broken"));

        var plugins = svc.Discover();

        plugins.Select(p => p.DisplayName).Should().ContainInOrder("Alpha", "Zeta");
        plugins.Should().HaveCount(2, "a folder without plugin.wasm/plugin.json is skipped");
        plugins[1].Repo.Should().Be("zeta/plugin");
        plugins[1].InstalledTag.Should().Be("v3");
        plugins[1].DirectoryName.Should().Be("zeta.plugin");
        plugins[0].Repo.Should().BeNull();
    }

    [Fact]
    public void FindByRepo_MatchesInstalledRepo_CaseInsensitive()
    {
        var (pluginsDir, svc) = NewSvc();
        WritePlugin(pluginsDir, "custom-dir", "Custom", "Owner/Repo");

        svc.FindByRepo("https://github.com/owner/repo")!.DisplayName.Should().Be("Custom");
        svc.FindByRepo("other/thing").Should().BeNull();
        svc.FindByRepo("not a repo").Should().BeNull();
    }

    [Fact]
    public void FindByRepo_FallsBackToDirectoryName_WhenNoInstallInfo()
    {
        var (pluginsDir, svc) = NewSvc();
        WritePlugin(pluginsDir, "owner.repo", "Hand Copied");

        svc.FindByRepo("owner/repo")!.DisplayName.Should().Be("Hand Copied");
    }

    [Fact]
    public void FindByName_MatchesDisplayNameOrDirectory()
    {
        var (pluginsDir, svc) = NewSvc();
        WritePlugin(pluginsDir, "owner.repo", "My Plugin");

        svc.FindByName("my plugin")!.DirectoryName.Should().Be("owner.repo");
        svc.FindByName("OWNER.REPO")!.DisplayName.Should().Be("My Plugin");
        svc.FindByName("nope").Should().BeNull();
        svc.FindByName(null).Should().BeNull();
    }

    [Fact]
    public void Run_MissingWasm_ReturnsNotFound()
    {
        var (_, svc) = NewSvc();

        svc.Run(new PluginDescriptor { WasmPath = "/does/not/exist.wasm", DisplayName = "x" }).Should().Be(PluginRunResult.NotFound);
        svc.Run(null).Should().Be(PluginRunResult.NotFound);
    }

    [Fact]
    public void Discover_MissingPluginsDirectory_ReturnsEmpty()
    {
        var svc = new PluginService(Path.Combine(_temp.Path, "nope"), _temp.Path, _temp.Path);

        svc.Discover().Should().BeEmpty();
    }
}
