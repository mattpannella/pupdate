using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class PatreonServiceTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly string _origBase;

    public PatreonServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origBase = PatreonService.PATREON_BASE;
        PatreonService.PATREON_BASE = _mock.BaseUrl;
    }

    public void Dispose()
    {
        PatreonService.PATREON_BASE = _origBase;
    }

    [Fact]
    public void TestSessionCookie_EmptyCookie_ReturnsInvalidWithMessage()
    {
        var diag = PatreonService.TestSessionCookie("", null);

        diag.CookieValid.Should().BeFalse();
        diag.Messages.Should().Contain(m => m.Contains("No session cookie"));
    }

    [Fact]
    public void TestSessionCookie_401_ReturnsRejectedMessage()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/api/current_user").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401));

        var diag = PatreonService.TestSessionCookie("expired-cookie", null);

        diag.CookieValid.Should().BeFalse();
        diag.Messages.Should().Contain(m => m.Contains("rejected"));
    }

    [Fact]
    public void TestSessionCookie_HappyPath_ReturnsValidWithUsername()
    {
        const string body = """
        {
          "data": { "id": "1", "type": "user", "attributes": { "full_name": "Jane Patron", "email": "jane@example.com" } },
          "included": []
        }
        """;
        _mock.Server
            .Given(Request.Create().WithPath("/api/current_user").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));

        var diag = PatreonService.TestSessionCookie("good-cookie", null);

        diag.CookieValid.Should().BeTrue();
        diag.PatreonUserName.Should().Be("Jane Patron");
        diag.Messages.Should().Contain(m => m.Contains("valid"));
    }

    [Fact]
    public void TestSessionCookie_NoIdentityInBody_ReturnsInvalid()
    {
        // data present, but attributes.full_name is missing → "no user identity" branch.
        _mock.Server
            .Given(Request.Create().WithPath("/api/current_user").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                """{ "data": { "id": "x", "type": "user", "attributes": { "email": "x@y.z" } } }"""));

        var diag = PatreonService.TestSessionCookie("ambiguous-cookie");

        diag.CookieValid.Should().BeFalse();
        diag.Messages.Should().Contain(m => m.Contains("no user identity"));
    }

    [Fact]
    public void TestSessionCookie_WithVanity_ResolvesCampaignIdFromHtml()
    {
        const string userBody = """
        {
          "data": { "id": "1", "type": "user", "attributes": { "full_name": "Jane Patron" } },
          "included": []
        }
        """;
        _mock.Server
            .Given(Request.Create().WithPath("/api/current_user").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(userBody));

        // Vanity HTML has campaign id embedded.
        _mock.Server
            .Given(Request.Create().WithPath("/jotego").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                """<html><script>{"id":"123456","type":"campaign"}</script></html>"""));

        _mock.Server
            .Given(Request.Create().WithPath("/api/posts").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{ "data": [] }"""));

        var diag = PatreonService.TestSessionCookie("good-cookie", "jotego");

        diag.CookieValid.Should().BeTrue();
        diag.CampaignId.Should().Be("123456");
        diag.PostsQueryReachable.Should().BeTrue();
    }

    [Fact]
    public void FetchAttachment_NullCookie_Throws()
    {
        var act = () => PatreonService.FetchAttachment(null, "jotego", "jtbeta.zip", out _);
        act.Should().Throw<Exception>().WithMessage("*session cookie is not set*");
    }

    [Fact]
    public void FetchAttachment_NullVanity_Throws()
    {
        var act = () => PatreonService.FetchAttachment("cookie", null, "jtbeta.zip", out _);
        act.Should().Throw<Exception>().WithMessage("*vanity*");
    }

    [Fact]
    public void FetchAttachment_NullFilename_Throws()
    {
        var act = () => PatreonService.FetchAttachment("cookie", "jotego", null, out _);
        act.Should().Throw<Exception>().WithMessage("*filename*");
    }

    [Fact]
    public void FetchAttachment_NoMatchingPost_Throws()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/jotego").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                """<html>{"id":"99","type":"campaign"}</html>"""));

        _mock.Server
            .Given(Request.Create().WithPath("/api/posts").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("""{ "data": [], "included": [] }"""));

        var act = () => PatreonService.FetchAttachment("cookie", "jotego", "jtbeta.zip", out _);

        act.Should().Throw<Exception>().WithMessage("*No recent*jtbeta.zip*");
    }

    [Fact]
    public void FetchAttachment_GatedPost_ThrowsWithSubscriptionMessage()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/jotego").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(
                """<html>{"id":"99","type":"campaign"}</html>"""));

        const string postsBody = """
        {
          "data": [
            {
              "id": "p1",
              "type": "post",
              "attributes": { "title": "Beta", "url": "/posts/p1", "current_user_can_view": false },
              "relationships": { "attachments_media": { "data": [ { "id": "m1", "type": "media" } ] } }
            }
          ],
          "included": [
            { "id": "m1", "type": "media", "attributes": { "file_name": "jtbeta.zip", "download_url": "http://x" } }
          ]
        }
        """;
        _mock.Server
            .Given(Request.Create().WithPath("/api/posts").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(postsBody));

        var act = () => PatreonService.FetchAttachment("cookie", "jotego", "jtbeta.zip", out _);

        act.Should().Throw<Exception>().WithMessage("*can't view*");
    }
}
