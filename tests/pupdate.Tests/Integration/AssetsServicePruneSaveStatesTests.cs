using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class AssetsServicePruneSaveStatesTests : IDisposable
{
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;

    public AssetsServicePruneSaveStatesTests(WireMockFixture mock)
    {
        // PruneSaveStates depends on ServiceHelper.UpdateDirectory and
        // ServiceHelper.SettingsService statics for its initial BackupMemories call,
        // so we need ServiceHelper initialized.
        mock.Reset();
        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);
        _fx = new OrchestrationFixture();

        // Minimal inventory + settings so ServiceHelper.Initialize succeeds.
        _fx.WriteInventory(Array.Empty<Core>(), Array.Empty<Platform>());
        _fx.WriteSettings();
        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);
    }

    public void Dispose()
    {
        Console.SetIn(_origStdin);
        _fx.Dispose();
    }

    private string SaveStatesDir(string coreName) =>
        Path.Combine(_fx.PocketDir, "Memories", "Save States", coreName);

    private string WriteState(string coreName, string filename)
    {
        string path = Path.Combine(SaveStatesDir(coreName), filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "save state bytes");
        return path;
    }

    [Fact]
    public void PruneSaveStates_KeepsOnlyMostRecentPerGame()
    {
        // Filename format: YYYYMMDD_HHMMSS_<author>_<id>_<gameName>.sta
        // 3 saves of "Mario" — only 20240301 should survive.
        WriteState("agg23.NES", "20240101_120000_aaa_b1_Mario.sta");
        WriteState("agg23.NES", "20240201_120000_aaa_b1_Mario.sta");
        WriteState("agg23.NES", "20240301_120000_aaa_b1_Mario.sta");
        // Different game — should be untouched (only one save).
        WriteState("agg23.NES", "20240101_120000_aaa_b1_Zelda.sta");

        AssetsService.PruneSaveStates(_fx.PocketDir);

        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240301_120000_aaa_b1_Mario.sta"))
            .Should().BeTrue("most recent Mario save must survive");
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240201_120000_aaa_b1_Mario.sta"))
            .Should().BeFalse();
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240101_120000_aaa_b1_Mario.sta"))
            .Should().BeFalse();
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240101_120000_aaa_b1_Zelda.sta"))
            .Should().BeTrue("the only Zelda save must survive");
    }

    [Fact]
    public void PruneSaveStates_NonMatchingFilenames_AreUntouched()
    {
        // Files that don't match the regex pattern should be left alone.
        WriteState("agg23.NES", "manual_backup.sta");
        WriteState("agg23.NES", "weirdname.sta");
        // And one matching file alongside, just to give the regex something to chew on.
        WriteState("agg23.NES", "20240301_120000_aaa_b1_Mario.sta");

        AssetsService.PruneSaveStates(_fx.PocketDir);

        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "manual_backup.sta"))
            .Should().BeTrue();
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "weirdname.sta"))
            .Should().BeTrue();
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240301_120000_aaa_b1_Mario.sta"))
            .Should().BeTrue();
    }

    [Fact]
    public void PruneSaveStates_CoreNameFilter_OnlyAffectsThatCore()
    {
        WriteState("agg23.NES", "20240101_120000_aaa_b1_Mario.sta");
        WriteState("agg23.NES", "20240301_120000_aaa_b1_Mario.sta");
        // Same game pattern in a *different* core — should NOT be pruned when coreName
        // limits to agg23.NES.
        WriteState("jotego.GG", "20240101_120000_jjj_g1_Sonic.sta");
        WriteState("jotego.GG", "20240301_120000_jjj_g1_Sonic.sta");

        AssetsService.PruneSaveStates(_fx.PocketDir, "agg23.NES");

        // agg23.NES — pruned.
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240101_120000_aaa_b1_Mario.sta"))
            .Should().BeFalse();
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240301_120000_aaa_b1_Mario.sta"))
            .Should().BeTrue();
        // jotego.GG — both kept because the filter excluded this core.
        File.Exists(Path.Combine(SaveStatesDir("jotego.GG"), "20240101_120000_jjj_g1_Sonic.sta"))
            .Should().BeTrue("coreName filter must skip this core entirely");
        File.Exists(Path.Combine(SaveStatesDir("jotego.GG"), "20240301_120000_jjj_g1_Sonic.sta"))
            .Should().BeTrue();
    }

    [Fact]
    public void PruneSaveStates_TimestampOrderingIsLexicographic_NotByValue()
    {
        // The implementation parses YYYYMMDD_HHMMSS as long and compares numerically — so
        // 20240301_120000 (>) 20240101_235959. Verify ordering across day/time boundaries.
        WriteState("agg23.NES", "20240101_235959_aaa_b1_Tetris.sta");  // late on Jan 1
        WriteState("agg23.NES", "20240301_000000_aaa_b1_Tetris.sta");  // early on Mar 1

        AssetsService.PruneSaveStates(_fx.PocketDir);

        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240301_000000_aaa_b1_Tetris.sta"))
            .Should().BeTrue("March 1st is the most recent");
        File.Exists(Path.Combine(SaveStatesDir("agg23.NES"), "20240101_235959_aaa_b1_Tetris.sta"))
            .Should().BeFalse();
    }
}
