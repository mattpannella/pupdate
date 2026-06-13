using Pannella.Helpers;
using WireMock.Server;
using WireMock.Settings;

namespace Pannella.Tests.Fixtures;

public class WireMockFixture : IDisposable
{
    public WireMockServer Server { get; }
    public string BaseUrl => Server.Urls[0];

    public WireMockFixture()
    {
        // Defensive: clear any inherited PUPDATE_LOCAL_FILES from the host shell so
        // integration tests reliably exercise remote-fetch code paths.
        Environment.SetEnvironmentVariable("PUPDATE_LOCAL_FILES", null);

        Server = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = false,
            StartAdminInterface = false
        });
    }

    public void Reset() => Server.Reset();

    public void Dispose()
    {
        try
        {
            Server.Stop();
            Server.Dispose();
        }
        catch
        {
            // best effort
        }

        HttpHelper.Reset();
        ServiceHelper.ResetForTests();

        GC.SuppressFinalize(this);
    }
}
