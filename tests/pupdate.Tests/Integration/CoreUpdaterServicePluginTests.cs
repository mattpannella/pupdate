using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pannella.Helpers;
using Pannella.Models.Events;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Models.Plugins;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoreUpdaterServicePluginTests : IDisposable
{
    private const string CoreId = "test.core";
    private const string PlatformId = "test_platform";
    private const string PlatformName = "Test Platform";
    private const string Version = "1.0.0";
    private const string PluginDir = "owner.repo";
    private const string PluginRepo = "owner/repo";
    private const string PluginName = "Test Plugin";

    private readonly WireMockFixture _mock;
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;
    private readonly IPluginService _plugins;
    private readonly PluginDescriptor _descriptor;

    public CoreUpdaterServicePluginTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();

        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);

        _fx = new OrchestrationFixture();

        _descriptor = new PluginDescriptor
        {
            DirectoryName = PluginDir,
            DisplayName = PluginName,
            Repo = PluginRepo,
            WasmPath = "plugin.wasm",
            InstalledTag = "v1"
        };

        _plugins = Substitute.For<IPluginService>();
        _plugins.Discover().Returns(new List<PluginDescriptor> { _descriptor });
    }

    public void Dispose()
    {
        Console.SetIn(_origStdin);
        CoreUpdaterService.InstallPluginPrompt = null;
        ServiceHelper.Unattended = false;
        _fx.Dispose();
    }

    private static Dictionary<string, PluginSettings> AutoRun(PluginPhase phase, string repo = PluginRepo) => new()
    {
        [PluginDir] = new PluginSettings { repo = repo, run_on_update = true, phase = phase }
    };

    private string StubCoreReleaseZipDownload(string updatersJson = null)
    {
        string zipPath = _fx.BuildCoreReleaseZip(CoreId, PlatformId, Version, PlatformName, updatersJson);
        var bytes = File.ReadAllBytes(zipPath);

        _mock.Server
            .Given(Request.Create().WithPath("/release.zip").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(bytes));

        return _mock.BaseUrl + "/release.zip";
    }

    private void WriteInventory(string updatersJson = null)
    {
        string downloadUrl = StubCoreReleaseZipDownload(updatersJson);
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(CoreId, PlatformId, Version, downloadUrl);
        _fx.WriteInventory(new[] { inventoryCore }, new[]
        {
            new Platform { id = PlatformId, category = "Console", name = PlatformName, manufacturer = "Test", year = 1990 }
        });
    }

    private (CoreUpdaterService updater, UpdateProcessCompleteEventArgs[] captured) BuildUpdater()
    {
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        var updater = new CoreUpdaterService(
            _fx.PocketDir,
            ServiceHelper.CoresService.Cores,
            ServiceHelper.FirmwareService,
            ServiceHelper.SettingsService,
            ServiceHelper.CoresService,
            _plugins);

        var captured = new UpdateProcessCompleteEventArgs[1];
        updater.UpdateProcessComplete += (_, e) => captured[0] = e;

        return (updater, captured);
    }

    private string CoreJsonPath => Path.Combine(_fx.PocketDir, "Cores", CoreId, "core.json");

    [Fact]
    public void BeforePhase_RunsBeforeCoresAreInstalled()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.before));
        bool? coreExistedWhenRun = null;
        _plugins.Run(_descriptor, Arg.Any<bool>()).Returns(_ =>
        {
            coreExistedWhenRun = File.Exists(CoreJsonPath);
            return PluginRunResult.Completed;
        });

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates();

        coreExistedWhenRun.Should().BeFalse("the before phase runs ahead of the core loop");
        File.Exists(CoreJsonPath).Should().BeTrue();
        captured[0]!.PluginsRun.Should().ContainSingle().Which.Should().Be(PluginName);
        _plugins.Received(1).Run(_descriptor, false);
    }

    [Fact]
    public void AfterPhase_RunsAfterCoresAreInstalled()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.after));
        bool? coreExistedWhenRun = null;
        _plugins.Run(_descriptor, Arg.Any<bool>()).Returns(_ =>
        {
            coreExistedWhenRun = File.Exists(CoreJsonPath);
            return PluginRunResult.Completed;
        });

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates();

        coreExistedWhenRun.Should().BeTrue("the after phase runs once the core loop is done");
        captured[0]!.PluginsRun.Should().ContainSingle();
    }

    [Fact]
    public void NotAutoRun_IsNeverRun()
    {
        WriteInventory();
        _fx.WriteSettings();

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates();

        _plugins.DidNotReceiveWithAnyArgs().Run(default, default);
        captured[0]!.PluginsRun.Should().BeEmpty();
    }

    [Fact]
    public void UpdateSelected_DoesNotRunPlugins()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.before));

        var (updater, _) = BuildUpdater();
        updater.RunUpdates(new[] { CoreId });

        _plugins.DidNotReceiveWithAnyArgs().Run(default, default);
        _plugins.DidNotReceiveWithAnyArgs().CheckForUpdate(default);
    }

    [Fact]
    public void Unattended_IsPassedThroughToRun()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.after));
        _plugins.Run(_descriptor, true).Returns(PluginRunResult.NeedsInput);

        var (updater, captured) = BuildUpdater();
        ServiceHelper.Unattended = true;
        updater.RunUpdates();

        _plugins.Received(1).Run(_descriptor, true);
        captured[0]!.PluginsNeedInput.Should().ContainSingle().Which.Should().Be(PluginName);
        captured[0]!.PluginsRun.Should().BeEmpty();
    }

    [Fact]
    public void ThrowingPlugin_IsReportedFailed_AndDoesNotAbortUpdate()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.before));
        _plugins.Run(_descriptor, Arg.Any<bool>()).Throws(new InvalidOperationException("boom"));

        var (updater, captured) = BuildUpdater();
        int errors = updater.RunUpdates();

        errors.Should().Be(0, "plugin failures are reported separately from core errors");
        captured[0]!.PluginsFailed.Should().ContainSingle().Which.Should().Be(PluginName);
        captured[0]!.InstalledCores.Should().ContainSingle(c => c["core"] == CoreId);
    }

    [Fact]
    public void UpdatePlugins_ChecksAndUpdatesEveryInstalledPlugin_EvenWhenNotAutoRun()
    {
        WriteInventory();
        _fx.WriteSettings(updatePlugins: true);
        _plugins.CheckForUpdate(_descriptor).Returns("v2");

        var (updater, _) = BuildUpdater();
        updater.RunUpdates();

        _plugins.Received(1).CheckForUpdate(_descriptor);
        _plugins.Received(1).Update(_descriptor);
    }

    [Fact]
    public void UpdatePlugins_UpToDate_DoesNotUpdate()
    {
        WriteInventory();
        _fx.WriteSettings(updatePlugins: true);
        _plugins.CheckForUpdate(_descriptor).Returns((string)null);

        var (updater, _) = BuildUpdater();
        updater.RunUpdates();

        _plugins.Received(1).CheckForUpdate(_descriptor);
        _plugins.DidNotReceiveWithAnyArgs().Update(default);
    }

    [Fact]
    public void UpdatePlugins_SkipsHandCopiedPluginsWithoutRepo()
    {
        WriteInventory();
        _fx.WriteSettings(updatePlugins: true);
        _plugins.Discover().Returns(new List<PluginDescriptor>
        {
            new() { DirectoryName = "local.plugin", DisplayName = "Local", WasmPath = "plugin.wasm" }
        });

        var (updater, _) = BuildUpdater();
        updater.RunUpdates();

        _plugins.DidNotReceiveWithAnyArgs().CheckForUpdate(default);
    }

    [Fact]
    public void UpdatePluginsOff_DoesNotCheck()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.after), updatePlugins: false);

        var (updater, _) = BuildUpdater();
        updater.RunUpdates();

        _plugins.DidNotReceiveWithAnyArgs().CheckForUpdate(default);
        _plugins.DidNotReceiveWithAnyArgs().Update(default);
    }

    [Fact]
    public void MissingAutoRunPlugin_IsInstalledFromItsRepo()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.after));
        _plugins.Discover().Returns(new List<PluginDescriptor>());

        var (updater, _) = BuildUpdater();
        updater.RunUpdates();

        _plugins.Received(1).InstallFromGithub(PluginRepo);
    }

    [Fact]
    public void MissingAutoRunPlugin_WithoutRepo_IsNotInstalled()
    {
        WriteInventory();
        _fx.WriteSettings(pluginSettings: AutoRun(PluginPhase.after, repo: null));
        _plugins.Discover().Returns(new List<PluginDescriptor>());

        var (updater, _) = BuildUpdater();
        updater.RunUpdates();

        _plugins.DidNotReceiveWithAnyArgs().InstallFromGithub(default);
    }

    private const string UpdatersWithPlugin = """{ "plugins": ["https://github.com/Owner/Repo"] }""";

    [Fact]
    public void FreshInstall_WithRecommendedPlugin_PromptsAndInstalls()
    {
        WriteInventory(UpdatersWithPlugin);
        _fx.WriteSettings();
        var prompts = new List<(string core, string repo)>();
        CoreUpdaterService.InstallPluginPrompt = (core, repo) =>
        {
            prompts.Add((core, repo));
            return true;
        };

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates(new[] { CoreId });

        prompts.Should().ContainSingle().Which.Should().Be((CoreId, "Owner/Repo"));
        _plugins.Received(1).InstallFromGithub("Owner/Repo");
        captured[0]!.RecommendedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void FreshInstall_WithRecommendedPlugin_Declined_IsListedInSummary()
    {
        WriteInventory(UpdatersWithPlugin);
        _fx.WriteSettings();
        CoreUpdaterService.InstallPluginPrompt = (_, _) => false;

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates(new[] { CoreId });

        _plugins.DidNotReceiveWithAnyArgs().InstallFromGithub(default);
        captured[0]!.RecommendedPlugins.Should().ContainSingle().Which.Should().Contain("Owner/Repo");
    }

    [Fact]
    public void FreshInstall_WithRecommendedPlugin_Unattended_LogsHintOnly()
    {
        WriteInventory(UpdatersWithPlugin);
        _fx.WriteSettings();
        bool prompted = false;
        CoreUpdaterService.InstallPluginPrompt = (_, _) => prompted = true;

        var (updater, captured) = BuildUpdater();
        ServiceHelper.Unattended = true;
        updater.RunUpdates(new[] { CoreId });

        prompted.Should().BeFalse();
        _plugins.DidNotReceiveWithAnyArgs().InstallFromGithub(default);
        captured[0]!.RecommendedPlugins.Should().ContainSingle().Which.Should().Contain("Owner/Repo");
    }

    [Fact]
    public void FreshInstall_WithRecommendedPlugin_AlreadyInstalled_DoesNotPrompt()
    {
        WriteInventory(UpdatersWithPlugin);
        _fx.WriteSettings();
        _plugins.FindByRepo("Owner/Repo").Returns(_descriptor);
        bool prompted = false;
        CoreUpdaterService.InstallPluginPrompt = (_, _) => prompted = true;

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates(new[] { CoreId });

        prompted.Should().BeFalse();
        captured[0]!.RecommendedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void CoreUpdate_NotFreshInstall_DoesNotPrompt()
    {
        string preInstalledDir = Path.Combine(_fx.PocketDir, "Cores", CoreId);
        Directory.CreateDirectory(preInstalledDir);
        File.WriteAllText(Path.Combine(preInstalledDir, "core.json"),
            $$"""
            {
              "core": {
                "magic": "APF_VER_1",
                "metadata": {
                  "platform_ids": ["{{PlatformId}}"],
                  "shortname": "{{CoreId}}",
                  "description": "Test",
                  "author": "test",
                  "url": "u",
                  "version": "0.9.0",
                  "date_release": "2024-01-01"
                },
                "framework": { "version_required": "0", "sleep_supported": false }
              }
            }
            """);
        WriteInventory(UpdatersWithPlugin);
        _fx.WriteSettings();
        bool prompted = false;
        CoreUpdaterService.InstallPluginPrompt = (_, _) => prompted = true;

        var (updater, captured) = BuildUpdater();
        updater.RunUpdates(new[] { CoreId });

        captured[0]!.InstalledCores.Should().ContainSingle(c => c["version"] == Version, "the core was updated");
        prompted.Should().BeFalse("recommendations only fire on a fresh install");
        captured[0]!.RecommendedPlugins.Should().BeEmpty();
    }
}
