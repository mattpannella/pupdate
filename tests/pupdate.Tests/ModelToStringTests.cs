using FluentAssertions;
using Pannella.Models.DisplayModes;
using Pannella.Models.Extras;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Xunit;

namespace pupdate.Tests;

public class ModelToStringTests
{
    [Fact]
    public void Platform_ToString_ReturnsName()
    {
        // Arrange
        var platform = new Platform
        {
            name = "Nintendo Entertainment System",
            category = "console",
            year = 1985,
            manufacturer = "Nintendo"
        };

        // Act
        var result = platform.ToString();

        // Assert
        result.Should().Be("Nintendo Entertainment System");
    }

    [Fact]
    public void Platform_ToString_WithNullName_ReturnsNull()
    {
        // Arrange
        var platform = new Platform
        {
            category = "console",
            year = 1985
        };

        // Act
        var result = platform.ToString();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Core_ToString_FormatsWithPlatformNameAndIdentifier()
    {
        // Arrange
        var core = new Core
        {
            identifier = "spiritualized.nes",
            platform = new Platform { name = "NES" }
        };

        // Act
        var result = core.ToString();

        // Assert
        result.Should().Be("NES (spiritualized.nes)");
    }

    [Fact]
    public void PocketExtra_ToString_ReturnsId()
    {
        // Arrange
        var extra = new PocketExtra
        {
            id = "gb-palettes",
            name = "GameBoy Palettes",
            description = "Color palettes for GB cores"
        };

        // Act
        var result = extra.ToString();

        // Assert
        result.Should().Be("gb-palettes");
    }

    [Fact]
    public void DisplayMode_ToString_FormatsValueOrderAndDescription()
    {
        // Arrange
        var displayMode = new DisplayMode
        {
            value = "1080p",
            description = "Full HD Resolution",
            order = 1
        };

        // Act
        var result = displayMode.ToString();

        // Assert
        result.Should().Be("1080p - 1 - Full HD Resolution");
    }

    [Fact]
    public void AnalogueDisplayMode_ToString_FormatsIdAndDescription()
    {
        // Arrange
        var displayMode = new Pannella.Models.Analogue.Video.DisplayMode
        {
            id = "1080p",
            description = "Full HD"
        };

        // Act
        var result = displayMode.ToString();

        // Assert
        result.Should().Be("1080p Full HD");
    }

    [Fact]
    public void AnalogueDisplayMode_ToString_WithoutDescription_ShowsOnlyId()
    {
        // Arrange
        var displayMode = new Pannella.Models.Analogue.Video.DisplayMode
        {
            id = "720p"
        };

        // Act
        var result = displayMode.ToString();

        // Assert
        result.Should().Be("720p ");
    }
}
