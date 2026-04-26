using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class GithubApiServiceTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly string _origReleases;
    private readonly string _origContents;

    public GithubApiServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origReleases = GithubApiService.RELEASES;
        _origContents = GithubApiService.CONTENTS;
        GithubApiService.RELEASES = _mock.BaseUrl + "/repos/{0}/{1}/releases";
        GithubApiService.CONTENTS = _mock.BaseUrl + "/repos/{0}/{1}/contents/{2}";
    }

    public void Dispose()
    {
        GithubApiService.RELEASES = _origReleases;
        GithubApiService.CONTENTS = _origContents;
    }

    [Fact]
    public void GetReleases_HappyPath_ReturnsParsedReleases()
    {
        const string body = """
        [
          { "tag_name": "v1.0.0", "name": "Release 1", "prerelease": false, "url": "u1", "html_url": "h1", "assets": [] },
          { "tag_name": "v2.0.0", "name": "Release 2", "prerelease": true,  "url": "u2", "html_url": "h2", "assets": [] }
        ]
        """;
        _mock.Server
            .Given(Request.Create().WithPath("/repos/foo/bar/releases").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

        var releases = GithubApiService.GetReleases("foo", "bar");

        releases.Should().HaveCount(2);
        releases[0].tag_name.Should().Be("v1.0.0");
        releases[1].prerelease.Should().BeTrue();
    }

    [Fact]
    public void GetLatestRelease_ReturnsParsedRelease()
    {
        const string body = """{ "tag_name": "v9.9.9", "name": "Latest", "prerelease": false, "assets": [] }""";
        _mock.Server
            .Given(Request.Create().WithPath("/repos/foo/bar/releases/latest").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

        var release = GithubApiService.GetLatestRelease("foo", "bar");

        release.tag_name.Should().Be("v9.9.9");
    }

    [Fact]
    public void GetRelease_ByTag_ReturnsParsedRelease()
    {
        const string body = """{ "tag_name": "v1.2.3", "name": "Tagged", "prerelease": false, "assets": [] }""";
        _mock.Server
            .Given(Request.Create().WithPath("/repos/foo/bar/releases/tags/v1.2.3").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

        var release = GithubApiService.GetRelease("foo", "bar", "v1.2.3");

        release.tag_name.Should().Be("v1.2.3");
    }

    [Fact]
    public void GetReleases_404_ThrowsHttpRequestException()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/repos/foo/missing/releases").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var act = () => GithubApiService.GetReleases("foo", "missing");
        act.Should().Throw<HttpRequestException>();
    }

    [Fact]
    public void RemainingCalls_TracksRateLimitHeader()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/repos/foo/ratelimit/releases/latest").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("x-ratelimit-remaining", "42")
                .WithBody("""{ "tag_name": "v1", "assets": [] }"""));

        GithubApiService.GetLatestRelease("foo", "ratelimit");

        GithubApiService.RemainingCalls.Should().Be(42);
    }

    [Fact]
    public void GetReleases_WithToken_SendsAuthorizationHeader()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/repos/auth/test/releases").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));

        GithubApiService.GetReleases("auth", "test", githubToken: "secret-token");

        var requests = _mock.Server.LogEntries
            .Where(e => e.RequestMessage.AbsolutePath == "/repos/auth/test/releases")
            .ToList();

        requests.Should().HaveCount(1);
        var headers = requests[0].RequestMessage.Headers;
        headers.Should().ContainKey("Authorization");
        headers["Authorization"].ToString().Should().Contain("token secret-token");
    }

    [Fact]
    public void GetReleases_WithoutToken_DoesNotSendAuthorizationHeader()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/repos/noauth/test/releases").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("[]"));

        GithubApiService.GetReleases("noauth", "test");

        var req = _mock.Server.LogEntries
            .First(e => e.RequestMessage.AbsolutePath == "/repos/noauth/test/releases");
        req.RequestMessage.Headers.Should().NotContainKey("Authorization");
    }

    [Fact]
    public void DownloadFileContents_ReturnsRawBytes_AndSendsAcceptRawHeader()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        _mock.Server
            .Given(Request.Create()
                .WithPath("/repos/raw/test/contents/path/to/file.bin")
                .WithHeader("Accept", new RegexMatcher(".*application/vnd\\.github\\.raw.*"))
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(bytes));

        var result = GithubApiService.DownloadFileContents("raw", "test", "path/to/file.bin");

        result.Should().Equal(bytes);
    }
}
