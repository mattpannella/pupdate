using FluentAssertions;
using Pannella;
using GithubRelease = Pannella.Models.Github.Release;

namespace Pannella.Tests.Unit;

public class SelectLatestReleaseTests
{
    private static List<GithubRelease> SampleReleases() => new()
    {
        new GithubRelease { tag_name = "5.1.0", draft = false },
        new GithubRelease { tag_name = "5.0.0", draft = false },
        new GithubRelease { tag_name = "4.14.2", draft = false },
        new GithubRelease { tag_name = "4.14.1", draft = false },
        new GithubRelease { tag_name = "5.9.0", draft = true }, // draft: must be ignored
    };

    [Fact]
    public void LegacyNet7Window_PicksHighest4xOnly()
    {
        // net7.0 legacy path: [4, 4] — pinned to the 4.x line, never offered 5.x.
        var target = Program.SelectLatestRelease(SampleReleases(), 4, 4);

        target.Should().NotBeNull();
        target!.tag_name.Should().Be("4.14.2");
    }

    [Fact]
    public void ModernNet9Window_PicksHighestOverall()
    {
        // net9.0 path: [0, int.MaxValue] — tracks the newest release overall (5.x), still skips drafts.
        var target = Program.SelectLatestRelease(SampleReleases(), 0, int.MaxValue);

        target.Should().NotBeNull();
        target!.tag_name.Should().Be("5.1.0");
    }

    [Fact]
    public void ReturnsNull_WhenNoReleaseMatchesWindow()
    {
        var releases = new List<GithubRelease>
        {
            new GithubRelease { tag_name = "5.0.0", draft = false },
        };

        // No 4.x release available for the legacy window.
        Program.SelectLatestRelease(releases, 4, 4).Should().BeNull();
    }

    [Fact]
    public void SkipsTagsWithoutParseableSemver()
    {
        var releases = new List<GithubRelease>
        {
            new GithubRelease { tag_name = "nightly", draft = false },
            new GithubRelease { tag_name = "4.14.2", draft = false },
        };

        Program.SelectLatestRelease(releases, 4, 4)!.tag_name.Should().Be("4.14.2");
    }
}
