using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Models.AiCheck;
using Pannella.Services;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Services;

public class CoresServiceLogicTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public CoresServiceLogicTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private CoresService BuildBare()
    {
        // Construct a CoresService with minimal deps. The IsAnalogizerVariant method
        // doesn't touch settings/archive/assets, so they can be null.
        string installPath = Path.Combine(_temp.Path, "pocket-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installPath);
        return new CoresService(installPath, settingsService: null, archiveService: null, assetsService: null);
    }

    [Theory]
    [InlineData("agg23.Analogizer.NES", true)]
    [InlineData("agg23.NES", false)]
    [InlineData("Analogizer", true)]
    [InlineData("agg23.AnalogizerVariant", true)]
    [InlineData("foo.bar", false)]
    public void IsAnalogizerVariant_ChecksForSubstring(string id, bool expected)
    {
        BuildBare().IsAnalogizerVariant(id).Should().Be(expected);
    }

    [Fact]
    public void IsAnalogizerVariant_NullIdentifier_Throws()
    {
        // Pin current behavior: identifier.Contains has no null guard
        var svc = BuildBare();
        var act = () => svc.IsAnalogizerVariant(null);
        act.Should().Throw<NullReferenceException>();
    }

    [Theory]
    [InlineData(0.81, 80, true)]   // over the threshold
    [InlineData(0.80, 80, false)]  // boundary: strictly greater, so equal is NOT filtered
    [InlineData(0.79, 80, false)]  // under the threshold
    [InlineData(1.0, 80, true)]
    [InlineData(0.51, 50, true)]
    [InlineData(0.50, 50, false)]  // boundary
    [InlineData(0.0, 0, false)]    // score 0 is not > 0%
    [InlineData(0.01, 0, true)]    // any positive score is over a 0% threshold
    public void ExceedsAiThreshold_IsStrictlyGreaterThanPercentage(double score, int thresholdPercent, bool expected)
    {
        CoresService.ExceedsAiThreshold(score, thresholdPercent).Should().Be(expected);
    }

    [Fact]
    public void AiReport_DeserializesEndpointShape_KeyedByCoreIdWithOverallScore()
    {
        // Mirrors the ai_report.json shape: a top-level object keyed by core id, extra fields ignored.
        var json = @"{
            ""HarpMudd.Mpatrol"": { ""last_run"": 1787158836133, ""results"": {}, ""overall_score"": 1.0 },
            ""HarpMudd.Mp3Player"": { ""results"": {}, ""overall_score"": 0.52 }
        }";

        var report = JsonConvert.DeserializeObject<Dictionary<string, AiCheckEntry>>(json);

        report.Should().ContainKey("HarpMudd.Mpatrol");
        report["HarpMudd.Mpatrol"].overall_score.Should().Be(1.0);
        report["HarpMudd.Mp3Player"].overall_score.Should().Be(0.52);
    }
}
