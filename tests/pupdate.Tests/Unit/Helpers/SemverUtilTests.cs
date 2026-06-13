using FluentAssertions;
using Pannella.Helpers;

namespace Pannella.Tests.Unit.Helpers;

public class SemverUtilTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("core_1.2.3_release", "1.2.3")]
    [InlineData("1.2.3-beta", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("v0.0.1", "0.0.1")]
    public void FindSemver_ExtractsExpectedVersion(string input, string expected)
    {
        SemverUtil.FindSemver(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("nodigits")]
    [InlineData("")]
    public void FindSemver_ReturnsNull_WhenNoVersionFound(string input)
    {
        SemverUtil.FindSemver(input).Should().BeNull();
    }

    [Fact]
    public void FindSemver_ReturnsFirstMatch_WhenMultipleVersionsPresent()
    {
        SemverUtil.FindSemver("v1.2.3 / v4.5.6").Should().Be("1.2.3");
    }

    [Theory]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.9", true)]
    public void SemverCompare_ReturnsTrue_WhenAGreaterThanB(string a, string b, bool expected)
    {
        SemverUtil.SemverCompare(a, b).Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0", "1.1.0")]
    public void SemverCompare_ReturnsFalse_WhenALessThanB(string a, string b)
    {
        SemverUtil.SemverCompare(a, b).Should().BeFalse();
    }

    [Fact]
    public void SemverCompare_ReturnsFalse_WhenAEqualsB()
    {
        // Pinned current behavior: equal versions return false (verA.CompareTo(verB) == 0 → false branch)
        SemverUtil.SemverCompare("1.2.3", "1.2.3").Should().BeFalse();
    }

    [Fact]
    public void SemverCompare_Throws_WhenInputMalformed()
    {
        var act = () => SemverUtil.SemverCompare("not.a.version", "1.0.0");
        act.Should().Throw<Exception>();
    }
}
