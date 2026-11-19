using FluentAssertions;
using Pannella.Helpers;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class HttpHelperLoginTests : IDisposable
{
    private readonly WireMockFixture _mock;

    public HttpHelperLoginTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        HttpHelper.Reset();
    }

    public void Dispose() => HttpHelper.Reset();

    private void StubLogin(string user, string sig)
    {
        string body = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = true,
            values = new { cookies = new Dictionary<string, string> { ["logged-in-user"] = user, ["logged-in-sig"] = sig } }
        });

        _mock.Server
            .Given(Request.Create().WithPath("/login").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(body));
    }

    [Fact]
    public void GetAuthCookie_AcceptsPlainCookieValues()
    {
        StubLogin("user%40example.com", "abc%20def");

        var act = () => HttpHelper.Instance.GetAuthCookie("u", "p", _mock.BaseUrl + "/login");

        act.Should().NotThrow();
    }

    [Fact]
    public void GetAuthCookie_StripsSetCookieAttributes()
    {
        const string attrs = "; expires=Mon, 08 Nov 2027 09:06:15 GMT; Max-Age=31536000; path=/; domain=.archive.org; HttpOnly";
        StubLogin("user%40example.com" + attrs, "1825664775%201794128775%20BPRUtZ2GKUF%2B42%3D%3D" + attrs);

        var act = () => HttpHelper.Instance.GetAuthCookie("u", "p", _mock.BaseUrl + "/login");

        act.Should().NotThrow();
    }
}
