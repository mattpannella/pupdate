using FluentAssertions;
using Pannella.Helpers;
using Xunit;

namespace pupdate.Tests;

public class UtilTests
{
    [Fact]
    public void WordWrap_SimpleText_WrapsCorrectly()
    {
        // Arrange
        var text = "This is a simple test";
        var width = 10;

        // Act
        var result = Util.WordWrap(text, width);

        // Assert
        result.Should().Contain("This is a ");
        result.Should().Contain("\n");
        result.Should().Contain("simple ");
        result.Should().Contain("test ");
    }

    [Fact]
    public void WordWrap_TextFitsInWidth_NoWrapping()
    {
        // Arrange
        var text = "Short";
        var width = 20;

        // Act
        var result = Util.WordWrap(text, width);

        // Assert
        result.Should().Be("Short ");
        result.Should().NotContain("\n");
    }

    [Fact]
    public void WordWrap_WithPadding_AddsPaddingAfterWrap()
    {
        // Arrange
        var text = "This is a test";
        var width = 10;
        var padding = "  ";

        // Act
        var result = Util.WordWrap(text, width, padding);

        // Assert
        result.Should().StartWith(padding);
        var lines = result.Split('\n');
        lines.Should().HaveCountGreaterThan(1);
        lines.Skip(1).Should().OnlyContain(line => line.StartsWith(padding) || string.IsNullOrWhiteSpace(line));
    }

    [Fact]
    public void WordWrap_EmptyString_ReturnsEmptyWithPadding()
    {
        // Arrange
        var text = "";
        var width = 10;
        var padding = ">>";

        // Act
        var result = Util.WordWrap(text, width, padding);

        // Assert
        result.Should().Be(">> ");
    }

    [Fact]
    public void GetExceptionMessage_SingleException_ReturnsMessage()
    {
        // Arrange
        var ex = new Exception("Test error");

        // Act
        var result = Util.GetExceptionMessage(ex);

        // Assert
        result.Should().Contain("Test error");
    }

    [Fact]
    public void GetExceptionMessage_NestedExceptions_ReturnsAllMessages()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner error");
        var middleEx = new ApplicationException("Middle error", innerEx);
        var outerEx = new Exception("Outer error", middleEx);

        // Act
        var result = Util.GetExceptionMessage(outerEx);

        // Assert
        result.Should().Contain("Outer error");
        result.Should().Contain("Middle error");
        result.Should().Contain("Inner error");
    }

    [Fact]
    public void GetExceptionMessage_NestedExceptions_MessagesInCorrectOrder()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");
        var outerEx = new Exception("Outer", innerEx);

        // Act
        var result = Util.GetExceptionMessage(outerEx);

        // Assert
        var indexOuter = result.IndexOf("Outer");
        var indexInner = result.IndexOf("Inner");
        indexOuter.Should().BeLessThan(indexInner, "outer exception should appear before inner");
    }
}
