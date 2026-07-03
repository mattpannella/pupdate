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
    public void MainGuard_PicksHighest5xAndIgnoresDraftsAnd4x()
    {
        // main build: [5, int.MaxValue]
        var target = Program.SelectLatestRelease(SampleReleases(), 5, int.MaxValue);

        target.Should().NotBeNull();
        target!.tag_name.Should().Be("5.1.0");
    }

    [Fact]
    public void Legacy4xWindow_PicksHighest4xOnly()
    {
        // net7.0 legacy path on the 4.x branch: [4, 4]
        var target = Program.SelectLatestRelease(SampleReleases(), 4, 4);

        target.Should().NotBeNull();
        target!.tag_name.Should().Be("4.14.2");
    }

    [Fact]
    public void Unbounded_PicksHighestOverall()
    {
        // net9.0 path on the 4.x branch: [0, int.MaxValue] (latest overall, still skips drafts)
        var target = Program.SelectLatestRelease(SampleReleases(), 0, int.MaxValue);

        target.Should().NotBeNull();
        target!.tag_name.Should().Be("5.1.0");
    }

    [Fact]
    public void ReturnsNull_WhenNoReleaseMatchesWindow()
    {
        var releases = new List<GithubRelease>
        {
            new GithubRelease { tag_name = "4.14.2", draft = false },
        };

        Program.SelectLatestRelease(releases, 5, int.MaxValue).Should().BeNull();
    }

    [Fact]
    public void SkipsTagsWithoutParseableSemver()
    {
        var releases = new List<GithubRelease>
        {
            new GithubRelease { tag_name = "nightly", draft = false },
            new GithubRelease { tag_name = "5.0.0", draft = false },
        };

        Program.SelectLatestRelease(releases, 5, int.MaxValue)!.tag_name.Should().Be("5.0.0");
    }
}
