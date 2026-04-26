using System.Text;
using FluentAssertions;
using Pannella.Helpers;
using Pannella.Tests.Fixtures;

namespace Pannella.Tests.Unit.Helpers;

public class UtilTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _temp;

    public UtilTests(TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    private string WriteFile(string name, byte[] content)
    {
        string path = Path.Combine(_temp.Path, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void CompareChecksum_Md5_ReturnsTrue_ForMatchingHash()
    {
        // "hello world" => MD5 5EB63BBBE01EEED093CB22BB8F5ACDC3
        string path = WriteFile("hello.txt", Encoding.UTF8.GetBytes("hello world"));
        Util.CompareChecksum(path, "5EB63BBBE01EEED093CB22BB8F5ACDC3", Util.HashTypes.MD5).Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_Md5_IsCaseInsensitive()
    {
        string path = WriteFile("hello-case.txt", Encoding.UTF8.GetBytes("hello world"));
        Util.CompareChecksum(path, "5eb63bbbe01eeed093cb22bb8f5acdc3", Util.HashTypes.MD5).Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_Md5_ReturnsFalse_ForMismatchedHash()
    {
        string path = WriteFile("hello-bad.txt", Encoding.UTF8.GetBytes("hello world"));
        Util.CompareChecksum(path, "00000000000000000000000000000000", Util.HashTypes.MD5).Should().BeFalse();
    }

    [Fact]
    public void CompareChecksum_Crc32_ReturnsTrue_ForMatchingHash()
    {
        // "hello world" => CRC32 0D4A1185
        string path = WriteFile("crc.txt", Encoding.UTF8.GetBytes("hello world"));
        Util.CompareChecksum(path, "0D4A1185", Util.HashTypes.CRC32).Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_DefaultsToCrc32()
    {
        string path = WriteFile("crc-default.txt", Encoding.UTF8.GetBytes("hello world"));
        Util.CompareChecksum(path, "0D4A1185").Should().BeTrue();
    }

    [Fact]
    public void CompareChecksum_Throws_WhenFileMissing()
    {
        string path = Path.Combine(_temp.Path, "does-not-exist.bin");
        var act = () => Util.CompareChecksum(path, "00000000");
        act.Should().Throw<Exception>().WithMessage("*doesn't exist*");
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("  v1.2.3", "1.2.3")]
    [InlineData("", "")]
    public void NormalizeVersionTag_StripsLeadingV(string input, string expected)
    {
        Util.NormalizeVersionTag(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeVersionTag_HandlesNull()
    {
        Util.NormalizeVersionTag(null).Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3", true)]
    [InlineData("1.2.3", "v1.2.3", true)]
    [InlineData("1.2.3", "1.2.3", true)]
    [InlineData("1.2.3", "1.2.4", false)]
    public void VersionsMatch_NormalizesBeforeComparing(string a, string b, bool expected)
    {
        Util.VersionsMatch(a, b).Should().Be(expected);
    }

    [Fact]
    public void VersionsMatch_NullVsAnything_IsFalse()
    {
        Util.VersionsMatch(null, "1.0.0").Should().BeFalse();
        Util.VersionsMatch("1.0.0", null).Should().BeFalse();
    }

    [Fact]
    public void VersionsMatch_NullVsNull_IsTrue()
    {
        Util.VersionsMatch(null, null).Should().BeTrue();
    }

    [Fact]
    public void GetExceptionMessage_SingleException_ReturnsMessage()
    {
        var ex = new Exception("top");
        Util.GetExceptionMessage(ex).Trim().Should().Be("top");
    }

    [Fact]
    public void GetExceptionMessage_NestedExceptions_JoinsAllMessages()
    {
        var inner2 = new Exception("inner2");
        var inner1 = new Exception("inner1", inner2);
        var top = new Exception("top", inner1);

        var msg = Util.GetExceptionMessage(top);

        msg.Should().Contain("top").And.Contain("inner1").And.Contain("inner2");
    }

    [Fact]
    public void WordWrap_ShortLine_ReturnsUnchanged()
    {
        var result = Util.WordWrap("hello world", 80);
        result.Should().NotContain("\n");
        result.Should().Contain("hello").And.Contain("world");
    }

    [Fact]
    public void WordWrap_LongLine_WrapsAtWordBoundary()
    {
        var result = Util.WordWrap("aaaa bbbb cccc dddd", 10);
        result.Should().Contain("\n");
    }

    [Fact]
    public void WordWrap_AppliesPadding_ToWrappedLines()
    {
        var result = Util.WordWrap("aaaa bbbb cccc dddd", 10, ">>");
        result.Should().StartWith(">>");
        result.Should().Contain("\n>>");
    }

    [Fact]
    public void CleanDir_RemovesMacosxAndDotPrefixedAndMraAndTxtFiles()
    {
        string scratch = Path.Combine(_temp.Path, "clean-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Directory.CreateDirectory(Path.Combine(scratch, "__MACOSX"));
        File.WriteAllText(Path.Combine(scratch, "__MACOSX", "leftover.txt"), "junk");
        File.WriteAllText(Path.Combine(scratch, ".DS_Store"), "");
        File.WriteAllText(Path.Combine(scratch, "info.txt"), "remove me");
        File.WriteAllText(Path.Combine(scratch, "config.mra"), "remove me");
        File.WriteAllText(Path.Combine(scratch, "keep.bin"), "keep me");

        Util.CleanDir(scratch);

        Directory.Exists(Path.Combine(scratch, "__MACOSX")).Should().BeFalse();
        File.Exists(Path.Combine(scratch, ".DS_Store")).Should().BeFalse();
        File.Exists(Path.Combine(scratch, "info.txt")).Should().BeFalse();
        File.Exists(Path.Combine(scratch, "config.mra")).Should().BeFalse();
        File.Exists(Path.Combine(scratch, "keep.bin")).Should().BeTrue();
    }

    [Fact]
    public void CleanDir_RecursesIntoSubdirectories()
    {
        string scratch = Path.Combine(_temp.Path, "clean-recursive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(scratch, "sub"));
        File.WriteAllText(Path.Combine(scratch, "sub", "junk.txt"), "junk");
        File.WriteAllText(Path.Combine(scratch, "sub", "good.bin"), "good");

        Util.CleanDir(scratch);

        File.Exists(Path.Combine(scratch, "sub", "junk.txt")).Should().BeFalse();
        File.Exists(Path.Combine(scratch, "sub", "good.bin")).Should().BeTrue();
    }

    [Fact]
    public void CleanDir_PreservesPlatformsFolder_WhenFlagSetAndExistingPlatformJsonPresent()
    {
        string source = Path.Combine(_temp.Path, "src-" + Guid.NewGuid().ToString("N"));
        string installPath = Path.Combine(_temp.Path, "install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(source, "Platforms"));
        Directory.CreateDirectory(Path.Combine(installPath, "Platforms"));
        File.WriteAllText(Path.Combine(source, "Platforms", "myplat.json"), "extracted");
        File.WriteAllText(Path.Combine(installPath, "Platforms", "myplat.json"), "existing");

        Util.CleanDir(source, installPath, preservePlatformsFolder: true, platform: "myplat");

        Directory.Exists(Path.Combine(source, "Platforms")).Should().BeFalse(
            "extracted Platforms dir is removed because the install already has myplat.json");
    }

    [Fact]
    public void CopyDirectory_CopiesAllFiles_AndReturnsCount()
    {
        string src = Path.Combine(_temp.Path, "src-copy-" + Guid.NewGuid().ToString("N"));
        string dst = Path.Combine(_temp.Path, "dst-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "a.txt"), "a");
        File.WriteAllText(Path.Combine(src, "sub", "b.txt"), "b");

        int count = Util.CopyDirectory(src, dst, recursive: true, overwrite: false);

        count.Should().Be(2);
        File.Exists(Path.Combine(dst, "a.txt")).Should().BeTrue();
        File.Exists(Path.Combine(dst, "sub", "b.txt")).Should().BeTrue();
    }

    [Fact]
    public void CopyDirectory_ThrowsWhenSourceMissing()
    {
        string src = Path.Combine(_temp.Path, "missing-source-" + Guid.NewGuid().ToString("N"));
        string dst = Path.Combine(_temp.Path, "dst-missing-" + Guid.NewGuid().ToString("N"));

        var act = () => Util.CopyDirectory(src, dst, recursive: true, overwrite: false);
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void CopyDirectory_OverwriteTrue_ReplacesExistingFile()
    {
        string src = Path.Combine(_temp.Path, "ovsrc-" + Guid.NewGuid().ToString("N"));
        string dst = Path.Combine(_temp.Path, "ovdst-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(src, "a.txt"), "new");
        File.WriteAllText(Path.Combine(dst, "a.txt"), "old");

        Util.CopyDirectory(src, dst, recursive: false, overwrite: true);

        File.ReadAllText(Path.Combine(dst, "a.txt")).Should().Be("new");
    }
}
