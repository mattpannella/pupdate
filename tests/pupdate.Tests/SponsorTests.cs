using FluentAssertions;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Xunit;

namespace pupdate.Tests;

public class SponsorTests
{
    [Fact]
    public void ToString_EmptySponsor_ReturnsEmptyString()
    {
        // Arrange
        var sponsor = new Sponsor();

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToString_SingleStringProperty_ReturnsFormattedString()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            patreon = "https://patreon.com/example"
        };

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().Contain("https://patreon.com/example");
        result.Should().EndWith(Environment.NewLine);
    }

    [Fact]
    public void ToString_MultipleStringProperties_ReturnsAllFormatted()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            patreon = "https://patreon.com/example",
            ko_fi = "https://ko-fi.com/example",
            liberapay = "https://liberapay.com/example"
        };

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().Contain("https://patreon.com/example");
        result.Should().Contain("https://ko-fi.com/example");
        result.Should().Contain("https://liberapay.com/example");
        result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().HaveCount(3);
    }

    [Fact]
    public void ToString_ListProperty_ReturnsEachItemOnNewLine()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            github = new List<string>
            {
                "user1",
                "user2",
                "user3"
            }
        };

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().Contain("user1");
        result.Should().Contain("user2");
        result.Should().Contain("user3");
        result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().HaveCount(3);
    }

    [Fact]
    public void ToString_MixedProperties_ReturnsAllFormatted()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            patreon = "https://patreon.com/example",
            github = new List<string> { "user1", "user2" },
            ko_fi = "https://ko-fi.com/example",
            custom = new List<string> { "https://custom1.com", "https://custom2.com" }
        };

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().Contain("https://patreon.com/example");
        result.Should().Contain("user1");
        result.Should().Contain("user2");
        result.Should().Contain("https://ko-fi.com/example");
        result.Should().Contain("https://custom1.com");
        result.Should().Contain("https://custom2.com");
        // 1 patreon + 2 github + 1 ko_fi + 2 custom = 6 lines
        result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().HaveCount(6);
    }

    [Fact]
    public void ToString_WithPadding_AddsPaddingToEachLine()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            patreon = "https://patreon.com/example",
            ko_fi = "https://ko-fi.com/example"
        };
        var padding = "  ";

        // Act
        var result = sponsor.ToString(padding);

        // Assert
        result.Should().Contain($"{padding}https://patreon.com/example");
        result.Should().Contain($"{padding}https://ko-fi.com/example");
        var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().OnlyContain(line => line.StartsWith(padding));
    }

    [Fact]
    public void ToString_WithPaddingAndLists_AddsPaddingToAllLines()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            github = new List<string> { "user1", "user2" },
            patreon = "https://patreon.com/example"
        };
        var padding = ">> ";

        // Act
        var result = sponsor.ToString(padding);

        // Assert
        result.Should().Contain($"{padding}user1");
        result.Should().Contain($"{padding}user2");
        result.Should().Contain($"{padding}https://patreon.com/example");
        var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().OnlyContain(line => line.StartsWith(padding));
    }

    [Fact]
    public void ToString_NullListValues_SkipsNullLists()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            patreon = "https://patreon.com/example",
            github = null, // Explicitly null
            custom = null  // Explicitly null
        };

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().Contain("https://patreon.com/example");
        result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().HaveCount(1); // Only patreon
    }

    [Fact]
    public void ToString_EmptyList_SkipsEmptyList()
    {
        // Arrange
        var sponsor = new Sponsor
        {
            patreon = "https://patreon.com/example",
            github = new List<string>() // Empty list, not null
        };

        // Act
        var result = sponsor.ToString();

        // Assert
        result.Should().Contain("https://patreon.com/example");
        result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().HaveCount(1); // Only patreon, empty list is skipped
    }
}
