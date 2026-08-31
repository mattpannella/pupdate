using FluentAssertions;
using Newtonsoft.Json;
using Pannella.Models.AiCheck;

namespace Pannella.Tests.Unit.Models;

/// <summary>
/// Shapes taken from the live report at
/// https://openfpga-library.github.io/openfpga-ai-check/ai_report.json — the per-check "score" is
/// either a bare string or a one-property object, which is what AiCheckScoreConverter exists for.
/// </summary>
public class AiCheckEntryTests
{
    private const string Report = """
    {
      "AwesomeDolphin.SpaceInvaders": {
        "last_run": 1788101168002,
        "results": {
          "CommitsCheck": [
            { "name": "Pre-AI check", "score": "GuaranteeHuman", "output": ["All commits before 2026"] }
          ],
          "ReadmeCheck": [],
          "ContributorsCheck": []
        },
        "overall_score": 0.0
      },
      "HarpMudd.Mp3Player": {
        "last_run": 1788101168889,
        "results": {
          "ContributorsCheck": [],
          "CommitsCheck": [
            {
              "name": "Co-author check",
              "score": { "SuspectedAi": 0.97 },
              "output": ["Of the last 100 commit messages 97 mention Claude"]
            }
          ],
          "ReadmeCheck": []
        },
        "overall_score": 0.97
      }
    }
    """;

    private static Dictionary<string, AiCheckEntry> Parse() =>
        JsonConvert.DeserializeObject<Dictionary<string, AiCheckEntry>>(Report);

    [Fact]
    public void Deserialize_StringScore_KeepsLabelWithNoValue()
    {
        var entry = Parse()["AwesomeDolphin.SpaceInvaders"];

        entry.overall_score.Should().Be(0.0);
        entry.last_run.Should().Be(1788101168002);

        var (category, result) = entry.AllResults.Single();

        category.Should().Be("CommitsCheck");
        result.name.Should().Be("Pre-AI check");
        result.score.label.Should().Be("GuaranteeHuman");
        result.score.value.Should().BeNull();
        result.score.ToString().Should().Be("human");
        result.output.Should().ContainSingle().Which.Should().Be("All commits before 2026");
    }

    [Fact]
    public void Deserialize_ObjectScore_KeepsLabelAndValue()
    {
        var entry = Parse()["HarpMudd.Mp3Player"];

        entry.overall_score.Should().Be(0.97);

        var (_, result) = entry.AllResults.Single();

        result.score.label.Should().Be("SuspectedAi");
        result.score.value.Should().Be(0.97);
        result.score.ToString().Should().Be("suspected AI 97%");
    }

    [Fact]
    public void AllResults_SkipsEmptyCategories()
    {
        Parse().Values.Should().OnlyContain(entry => entry.AllResults.Count() == 1);
    }

    [Fact]
    public void Deserialize_MissingOrNullScore_DoesNotThrow()
    {
        var json = """
        { "Some.Core": { "overall_score": 0.5, "results": { "ReadmeCheck": [
            { "name": "no score", "score": null, "output": [] } ] } } }
        """;

        var entry = JsonConvert.DeserializeObject<Dictionary<string, AiCheckEntry>>(json)["Some.Core"];

        entry.AllResults.Single().Result.score.Should().BeNull();
        entry.last_run.Should().Be(0);
    }
}
