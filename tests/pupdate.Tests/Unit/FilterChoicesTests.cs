using FluentAssertions;
using Pannella.TUI;

namespace Pannella.Tests.Unit;

public class FilterChoicesTests
{
    private static string PlatformName(string id) => id switch
    {
        "psx" => "PlayStation",
        "playstation" => "PlayStation",
        "nes" => "NES",
        _ => id
    };

    [Fact]
    public void DuplicateDisplayNames_GetTheKeyAppended()
    {
        var choices = FilterChoices.Build(new[] { "psx", "nes", "playstation", "psx" }, PlatformName);

        choices.Should().Equal(
            ("nes", "NES"),
            ("playstation", "PlayStation (playstation)"),
            ("psx", "PlayStation (psx)"));
        choices.Select(c => c.Display).Should().OnlyHaveUniqueItems("labels are how the dropdown maps back to keys");
    }

    [Fact]
    public void UniqueDisplayNames_AreLeftAlone_AndSortedCaseInsensitively()
    {
        var choices = FilterChoices.Build(new[] { "zz", "", null, "Aa", "mm" }, k => k);

        choices.Should().Equal(("Aa", "Aa"), ("mm", "mm"), ("zz", "zz"));
    }

    [Fact]
    public void CaseOnlyCollisions_AreDisambiguatedToo()
    {
        var choices = FilterChoices.Build(new[] { "console", "Console" }, k => char.ToUpperInvariant(k[0]) + k[1..]);

        choices.Select(c => c.Display).Should().BeEquivalentTo("Console (console)", "Console (Console)");
    }
}
