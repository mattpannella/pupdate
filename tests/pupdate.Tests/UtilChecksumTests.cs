using FluentAssertions;
using Pannella.Helpers;
using Xunit;

namespace pupdate.Tests;

public class UtilChecksumTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly string _testContent = "Hello, World!";

    public UtilChecksumTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_testFilePath, _testContent);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void CompareChecksum_CRC32_MatchingChecksum_ReturnsTrue()
    {
        // Arrange
        // CRC32 of "Hello, World!" is 0xec4ac3d0
        var expectedChecksum = "ec4ac3d0";

        // Act
        var result = Util.CompareChecksum(_testFilePath, expectedChecksum, Util.HashTypes.CRC32);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_CRC32_NonMatchingChecksum_ReturnsFalse()
    {
        // Arrange
        var wrongChecksum = "00000000";

        // Act
        var result = Util.CompareChecksum(_testFilePath, wrongChecksum, Util.HashTypes.CRC32);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CompareChecksum_MD5_MatchingChecksum_ReturnsTrue()
    {
        // Arrange
        // MD5 of "Hello, World!" is 65a8e27d8879283831b664bd8b7f0ad4
        var expectedChecksum = "65a8e27d8879283831b664bd8b7f0ad4";

        // Act
        var result = Util.CompareChecksum(_testFilePath, expectedChecksum, Util.HashTypes.MD5);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_MD5_NonMatchingChecksum_ReturnsFalse()
    {
        // Arrange
        var wrongChecksum = "00000000000000000000000000000000";

        // Act
        var result = Util.CompareChecksum(_testFilePath, wrongChecksum, Util.HashTypes.MD5);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CompareChecksum_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var checksumLowercase = "ec4ac3d0";
        var checksumUppercase = "EC4AC3D0";

        // Act
        var resultLower = Util.CompareChecksum(_testFilePath, checksumLowercase, Util.HashTypes.CRC32);
        var resultUpper = Util.CompareChecksum(_testFilePath, checksumUppercase, Util.HashTypes.CRC32);

        // Assert
        resultLower.Should().BeTrue();
        resultUpper.Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_FileDoesNotExist_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt");

        // Act
        Action act = () => Util.CompareChecksum(nonExistentPath, "ec4ac3d0", Util.HashTypes.CRC32);

        // Assert
        act.Should().Throw<Exception>()
            .WithMessage("File doesn't exist. Cannot compute checksum.");
    }
}
