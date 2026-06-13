using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoresServiceReplaceIntegrationTests : IDisposable
{
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;

    public CoresServiceReplaceIntegrationTests(WireMockFixture mock)
    {
        // mock isn't used — ReplaceCheck is fully filesystem-driven — but the fixture forces
        // serialization with other HttpStateful tests so we don't fight ServiceHelper statics.
        mock.Reset();
        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);
        _fx = new OrchestrationFixture();
    }

    public void Dispose()
    {
        Console.SetIn(_origStdin);
        _fx.Dispose();
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void ReplaceCheck_OldCoreInstalled_MigratesAssetsSavesSettings_AndUninstallsOld()
    {
        // Arrange — new.NES replaces old.NES per its updaters.json "previous" array.
        const string oldId = "old.NES";
        const string newId = "new.NES";
        const string platformId = "nes";

        // Old core "installed" — has a core.json so IsInstalled returns true.
        WriteFile(Path.Combine(_fx.PocketDir, "Cores", oldId, "core.json"),
            $$"""{ "core": { "magic": "x", "metadata": { "platform_ids": ["{{platformId}}"] }, "framework": { "version_required":"0","sleep_supported":false } } }""");

        // GetLocalCores (called transitively by the Cores getter) scans every Cores/* dir
        // and reads Platforms/{platform_id}.json for any directory not in the inventory.
        // Old.NES isn't in the inventory, so that lookup must succeed.
        WriteFile(Path.Combine(_fx.PocketDir, "Platforms", $"{platformId}.json"),
            """{ "platform": { "category": "Console", "name": "Test", "manufacturer": "X", "year": 1990 } }""");

        // New core has updaters.json declaring old.NES as a previous identifier.
        WriteFile(Path.Combine(_fx.PocketDir, "Cores", newId, "updaters.json"),
            $$"""
            {
              "previous": [
                { "shortname": "NES", "author": "old", "platform_id": "{{platformId}}" }
              ]
            }
            """);

        // Pre-existing user data under the OLD core's identifier.
        WriteFile(Path.Combine(_fx.PocketDir, "Assets", platformId, oldId, "rom.bin"), "ROM");
        WriteFile(Path.Combine(_fx.PocketDir, "Saves", platformId, oldId, "save.dat"), "SAVE");
        WriteFile(Path.Combine(_fx.PocketDir, "Settings", oldId, "config.json"), "CONFIG");

        // Inventory must contain at least the new core so RefreshInstalledCores (called via
        // Uninstall) has a non-null CORES list to iterate.
        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            newId, platformId, "1.0.0", "http://localhost/unused.zip");
        var platform = new Platform
        {
            id = platformId, category = "Console", name = "Test", manufacturer = "X", year = 1990
        };
        _fx.WriteInventory(new[] { inventoryCore }, new[] { platform });
        _fx.WriteSettings();

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        // Act
        ServiceHelper.CoresService.ReplaceCheck(newId);

        // Assert — files moved from old id to new id.
        File.Exists(Path.Combine(_fx.PocketDir, "Assets", platformId, newId, "rom.bin"))
            .Should().BeTrue("Assets dir should be migrated to the new identifier");
        File.Exists(Path.Combine(_fx.PocketDir, "Saves", platformId, newId, "save.dat"))
            .Should().BeTrue("Saves dir should be migrated");
        File.Exists(Path.Combine(_fx.PocketDir, "Settings", newId, "config.json"))
            .Should().BeTrue("Settings dir should be migrated");

        Directory.Exists(Path.Combine(_fx.PocketDir, "Assets", platformId, oldId))
            .Should().BeFalse("old Assets dir should be moved away");
        Directory.Exists(Path.Combine(_fx.PocketDir, "Saves", platformId, oldId))
            .Should().BeFalse();
        Directory.Exists(Path.Combine(_fx.PocketDir, "Settings", oldId))
            .Should().BeFalse();

        // Old core uninstalled — its Cores/{oldId} dir is gone.
        Directory.Exists(Path.Combine(_fx.PocketDir, "Cores", oldId))
            .Should().BeFalse("old core should be uninstalled by ReplaceCheck");
    }

    [Fact]
    public void ReplaceCheck_OldCoreNotInstalled_IsNoOp()
    {
        // Arrange — new core has substitutes pointing at an old core that ISN'T installed.
        const string newId = "new.NES";
        const string platformId = "nes";

        WriteFile(Path.Combine(_fx.PocketDir, "Cores", newId, "updaters.json"),
            $$"""
            {
              "previous": [
                { "shortname": "NES", "author": "old", "platform_id": "{{platformId}}" }
              ]
            }
            """);

        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            newId, platformId, "1.0.0", "http://localhost/unused.zip");
        var platform = new Platform
        {
            id = platformId, category = "Console", name = "Test", manufacturer = "X", year = 1990
        };
        _fx.WriteInventory(new[] { inventoryCore }, new[] { platform });
        _fx.WriteSettings();

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        // Act + Assert — should not throw and should not create stray dirs.
        var act = () => ServiceHelper.CoresService.ReplaceCheck(newId);
        act.Should().NotThrow();

        Directory.Exists(Path.Combine(_fx.PocketDir, "Assets", platformId)).Should().BeFalse();
        Directory.Exists(Path.Combine(_fx.PocketDir, "Saves", platformId)).Should().BeFalse();
    }

    [Fact]
    public void ReplaceCheck_NoUpdatersJson_IsNoOp()
    {
        // Arrange — new core has no updaters.json at all.
        const string newId = "new.NES";
        const string platformId = "nes";

        Directory.CreateDirectory(Path.Combine(_fx.PocketDir, "Cores", newId));

        var inventoryCore = OrchestrationFixture.BuildInventoryCore(
            newId, platformId, "1.0.0", "http://localhost/unused.zip");
        var platform = new Platform
        {
            id = platformId, category = "Console", name = "Test", manufacturer = "X", year = 1990
        };
        _fx.WriteInventory(new[] { inventoryCore }, new[] { platform });
        _fx.WriteSettings();

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        var act = () => ServiceHelper.CoresService.ReplaceCheck(newId);
        act.Should().NotThrow();
    }
}
