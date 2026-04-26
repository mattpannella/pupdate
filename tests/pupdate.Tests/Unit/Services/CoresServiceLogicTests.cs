using FluentAssertions;
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
}
