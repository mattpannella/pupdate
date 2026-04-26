using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoinOpServiceTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly string _origEndpoint;

    public CoinOpServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origEndpoint = CoinOpService.LICENSE_ENDPOINT;
        CoinOpService.LICENSE_ENDPOINT = _mock.BaseUrl + "/?username={0}";
    }

    public void Dispose()
    {
        CoinOpService.LICENSE_ENDPOINT = _origEndpoint;
    }

    [Fact]
    public void FetchLicense_HappyPath_ReturnsBytes()
    {
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        _mock.Server
            .Given(Request.Create().WithPath("/").WithParam("username", "user@example.com").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));

        var result = CoinOpService.FetchLicense("user@example.com");

        result.Should().Equal(payload);
    }

    [Fact]
    public void FetchLicense_404_ThrowsWithResponseBody()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404).WithBody("user not found"));

        var act = () => CoinOpService.FetchLicense("missing@example.com");

        act.Should().Throw<Exception>().WithMessage("user not found");
    }

    [Fact]
    public void FetchLicense_500_ThrowsGenericMessage()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var act = () => CoinOpService.FetchLicense("any@example.com");

        act.Should().Throw<Exception>().WithMessage("Didn't work");
    }

    [Fact]
    public void FetchLicense_UrlEncodesEmail_PlusSign()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(new byte[] { 0x01 }));

        CoinOpService.FetchLicense("name+tag@example.com");

        var req = _mock.Server.LogEntries.First();
        // System.Web.HttpUtility.UrlEncode encodes + as %2b
        req.RequestMessage.RawQuery.Should().Contain("name%2btag");
    }
}
