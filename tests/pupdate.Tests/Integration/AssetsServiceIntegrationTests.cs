using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class AssetsServiceIntegrationTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly TempDirectoryFixture _temp;
    private readonly string _origEndpoint;
    private readonly string _origCwd;

    public AssetsServiceIntegrationTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origEndpoint = AssetsService.BLACKLIST_END_POINT;
        AssetsService.BLACKLIST_END_POINT = _mock.BaseUrl + "/blacklist.json";
        _temp = new TempDirectoryFixture();
        _origCwd = Directory.GetCurrentDirectory();
        // Move CWD into a clean temp dir so a stray local blacklist.json doesn't interfere.
        Directory.SetCurrentDirectory(_temp.Path);
    }

    public void Dispose()
    {
        AssetsService.BLACKLIST_END_POINT = _origEndpoint;
        try { Directory.SetCurrentDirectory(_origCwd); } catch { /* best effort */ }
        _temp.Dispose();
    }

    [Fact]
    public void RemoteBlacklist_FetchedAndParsed_WhenUseLocalIsFalse()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/blacklist.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""[ "*.bad", "specific-bad.bin" ]"""));

        var svc = new AssetsService(useLocalBlacklist: false, showStackTraces: false);

        svc.IsBlacklisted("foo.bad").Should().BeTrue();
        svc.IsBlacklisted("specific-bad.bin").Should().BeTrue();
        svc.IsBlacklisted("good.bin").Should().BeFalse();
    }

    [Fact]
    public void RemoteBlacklist_500_FallsBackToEmptyList_NoThrow()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/blacklist.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var svc = new AssetsService(useLocalBlacklist: false, showStackTraces: false);

        svc.IsBlacklisted("anything.bin").Should().BeFalse();
        svc.Blacklist.Should().BeEmpty();
    }
}
