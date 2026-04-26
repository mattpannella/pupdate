using FluentAssertions;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Models.Settings;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class CoresServiceRetrieveKeysTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly OrchestrationFixture _fx;
    private readonly TextReader _origStdin;
    private readonly string _origCoinOpEndpoint;

    public CoresServiceRetrieveKeysTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        _origStdin = Console.In;
        Console.SetIn(TextReader.Null);

        _origCoinOpEndpoint = CoinOpService.LICENSE_ENDPOINT;
        CoinOpService.LICENSE_ENDPOINT = _mock.BaseUrl + "/?username={0}";

        _fx = new OrchestrationFixture();
    }

    public void Dispose()
    {
        CoinOpService.LICENSE_ENDPOINT = _origCoinOpEndpoint;
        Console.SetIn(_origStdin);
        _fx.Dispose();
    }

    private void WriteSettingsWithCoinOp(bool enabled, string email)
    {
        // Build settings with the same defaults as OrchestrationFixture.WriteSettings, but
        // override coin_op_beta and patreon_email_address.
        var settings = new Settings();
        settings.config.download_firmware = false;
        settings.config.download_assets = false;
        settings.config.backup_saves = false;
        settings.config.crc_check = false;
        settings.config.jt_beta_github_fetch = false;
        settings.config.jt_beta_patreon_fetch = false;
        settings.config.use_local_cores_inventory = true;
        settings.config.use_local_blacklist = true;
        settings.config.use_local_pocket_extras = true;
        settings.config.use_local_display_modes = true;
        settings.config.use_local_ignore_instance_json = true;
        settings.config.use_local_pocket_library_images = true;
        settings.config.coin_op_beta = enabled;
        settings.config.patreon_email_address = email;

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented,
            new Newtonsoft.Json.JsonSerializerSettings { ContractResolver = ArchiveContractResolver.INSTANCE });
        File.WriteAllText(Path.Combine(_fx.SettingsDir, "pupdate_settings.json"), json);
    }

    [Fact]
    public void RetrieveKeys_CoinOpEnabled_WithEmail_FetchesAndWritesKey()
    {
        // Arrange
        var licenseBytes = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        _mock.Server
            .Given(Request.Create().WithPath("/").WithParam("username", "user@example.com").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(licenseBytes));

        _fx.WriteInventory(Array.Empty<Core>(), Array.Empty<Platform>());
        WriteSettingsWithCoinOp(enabled: true, email: "user@example.com");

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        // Act
        ServiceHelper.CoresService.RetrieveKeys();

        // Assert
        string keyPath = Path.Combine(_fx.PocketDir, "Licenses", "coinop.key");
        File.Exists(keyPath).Should().BeTrue();
        File.ReadAllBytes(keyPath).Should().Equal(licenseBytes);
    }

    [Fact]
    public void RetrieveKeys_CoinOpDisabled_DoesNotFetch()
    {
        _mock.Server
            .Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(new byte[] { 0x01 }));

        _fx.WriteInventory(Array.Empty<Core>(), Array.Empty<Platform>());
        WriteSettingsWithCoinOp(enabled: false, email: "user@example.com");

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        ServiceHelper.CoresService.RetrieveKeys();

        File.Exists(Path.Combine(_fx.PocketDir, "Licenses", "coinop.key"))
            .Should().BeFalse();
        _mock.Server.LogEntries.Should().BeEmpty(
            "coin_op_beta=false must short-circuit before any HTTP call");
    }

    [Fact]
    public void RetrieveKeys_CoinOpFails_DoesNotThrow_AndDoesNotWriteKey()
    {
        // Server returns 500 — CoinOpService throws "Didn't work", which RetrieveKeys catches.
        _mock.Server
            .Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        _fx.WriteInventory(Array.Empty<Core>(), Array.Empty<Platform>());
        WriteSettingsWithCoinOp(enabled: true, email: "user@example.com");

        ServiceHelper.Initialize(_fx.PocketDir, _fx.SettingsDir);

        var act = () => ServiceHelper.CoresService.RetrieveKeys();
        act.Should().NotThrow("RetrieveKeys swallows CoinOp errors with a stderr message");

        File.Exists(Path.Combine(_fx.PocketDir, "Licenses", "coinop.key"))
            .Should().BeFalse();
    }
}
