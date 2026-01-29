using System.Collections;
using FluentAssertions;
using Pannella.Helpers;
using Xunit;

namespace pupdate.Tests;

public class ReverseComparerTests
{
    [Fact]
    public void Compare_SmallerToLarger_ReturnsPositive()
    {
        // Arrange
        IComparer comparer = new ReverseComparer();

        // Act
        var result = comparer.Compare("a", "z");

        // Assert
        result.Should().BePositive("because reverse comparer inverts normal comparison");
    }

    [Fact]
    public void Compare_LargerToSmaller_ReturnsNegative()
    {
        // Arrange
        IComparer comparer = new ReverseComparer();

        // Act
        var result = comparer.Compare("z", "a");

        // Assert
        result.Should().BeNegative("because reverse comparer inverts normal comparison");
    }

    [Fact]
    public void Compare_EqualValues_ReturnsZero()
    {
        // Arrange
        IComparer comparer = new ReverseComparer();

        // Act
        var result = comparer.Compare("test", "test");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Compare_CaseInsensitive_TreatsUpperAndLowerAsSame()
    {
        // Arrange
        IComparer comparer = new ReverseComparer();

        // Act
        var result = comparer.Compare("Test", "test");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Compare_Numbers_WorksCorrectly()
    {
        // Arrange
        IComparer comparer = new ReverseComparer();

        // Act
        var result = comparer.Compare(1, 10);

        // Assert
        result.Should().BePositive("because 1 < 10, and reverse comparer inverts this");
    }
}
