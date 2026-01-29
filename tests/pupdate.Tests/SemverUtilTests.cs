using FluentAssertions;
using Pannella.Helpers;
using Xunit;

namespace pupdate.Tests;

public class SemverUtilTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("release-1.2.3", "1.2.3")]
    [InlineData("v1.2.3-beta", "1.2.3")]
    [InlineData("core_v1.0.5", "1.0.5")]
    [InlineData("2024.12.15", "2024.12.15")]
    public void FindSemver_ValidInputs_ExtractsVersion(string input, string expected)
    {
        // Act
        var result = SemverUtil.FindSemver(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("v1.2", "1.2.0")]
    [InlineData("2.5", "2.5.0")]
    public void FindSemver_TwoPartVersion_CompletesToThreePart(string input, string expected)
    {
        // Act
        var result = SemverUtil.FindSemver(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("no-version-here")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("v")]
    public void FindSemver_InvalidInputs_ReturnsNull(string input)
    {
        // Act
        var result = SemverUtil.FindSemver(input);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("1.2.3", "1.2.2", true)]  // A > B
    [InlineData("2.0.0", "1.9.9", true)]  // A > B
    [InlineData("1.3.0", "1.2.9", true)]  // A > B
    [InlineData("1.2.3", "1.2.3", false)] // A == B
    [InlineData("1.2.2", "1.2.3", false)] // A < B
    [InlineData("1.9.9", "2.0.0", false)] // A < B
    [InlineData("1.2.9", "1.3.0", false)] // A < B
    public void SemverCompare_VariousVersions_ReturnsCorrectResult(string versionA, string versionB, bool expected)
    {
        // Act
        var result = SemverUtil.SemverCompare(versionA, versionB);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("10.0.0", "2.0.0", true)]  // Test numeric comparison (not string)
    [InlineData("1.10.0", "1.2.0", true)]  // Test numeric comparison (not string)
    public void SemverCompare_NumericComparison_NotStringComparison(string versionA, string versionB, bool expected)
    {
        // Act
        var result = SemverUtil.SemverCompare(versionA, versionB);

        // Assert
        result.Should().Be(expected);
    }
}
