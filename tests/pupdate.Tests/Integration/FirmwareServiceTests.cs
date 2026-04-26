using System.Text;
using FluentAssertions;
using Pannella.Services;
using Pannella.Tests.Fixtures;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Pannella.Tests.Integration;

[Collection(HttpStateCollection.Name)]
public class FirmwareServiceTests : IDisposable
{
    private readonly WireMockFixture _mock;
    private readonly TempDirectoryFixture _temp;
    private readonly string _origBaseUrl;

    public FirmwareServiceTests(WireMockFixture mock)
    {
        _mock = mock;
        _mock.Reset();
        FirmwareService.ResetLatest();
        _origBaseUrl = FirmwareService.BASE_URL;
        // BASE_URL is concatenated with DETAILS = "support/pocket/firmware/{0}/details" so it must end with /
        FirmwareService.BASE_URL = _mock.BaseUrl + "/";
        _temp = new TempDirectoryFixture();
    }

    public void Dispose()
    {
        FirmwareService.BASE_URL = _origBaseUrl;
        FirmwareService.ResetLatest();
        _temp.Dispose();
    }

    private static string Md5Hex(byte[] bytes)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(bytes));
    }

    [Fact]
    public void UpdateFirmware_DownloadsFile_AndReturnsFilename_WhenMissing()
    {
        var firmwareBytes = Encoding.UTF8.GetBytes("FAKE_FIRMWARE_v2.2");
        var md5 = Md5Hex(firmwareBytes);
        string downloadUrl = _mock.BaseUrl + "/files/pocket_firmware_v2_2.bin";

        _mock.Server
            .Given(Request.Create().WithPath("/support/pocket/firmware/latest/details").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody($$"""{ "download_url": "{{downloadUrl}}", "md5": "{{md5}}" }"""));

        _mock.Server
            .Given(Request.Create().WithPath("/files/pocket_firmware_v2_2.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(firmwareBytes));

        var svc = new FirmwareService();
        string result = svc.UpdateFirmware(_temp.Path);

        result.Should().Be("pocket_firmware_v2_2.bin");
        File.Exists(Path.Combine(_temp.Path, "pocket_firmware_v2_2.bin")).Should().BeTrue();
        File.ReadAllBytes(Path.Combine(_temp.Path, "pocket_firmware_v2_2.bin"))
            .Should().Equal(firmwareBytes);
    }

    [Fact]
    public void UpdateFirmware_SkipsDownload_WhenFileMatchesMd5()
    {
        var firmwareBytes = Encoding.UTF8.GetBytes("FAKE_FIRMWARE_uptodate");
        var md5 = Md5Hex(firmwareBytes);
        string filename = "pocket_firmware_v2_3.bin";
        File.WriteAllBytes(Path.Combine(_temp.Path, filename), firmwareBytes);

        string downloadUrl = _mock.BaseUrl + "/files/" + filename;

        _mock.Server
            .Given(Request.Create().WithPath("/support/pocket/firmware/latest/details").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody($$"""{ "download_url": "{{downloadUrl}}", "md5": "{{md5}}" }"""));

        // Note: we DON'T stub the binary URL — if the service tries to fetch it, the test fails.

        var svc = new FirmwareService();
        string result = svc.UpdateFirmware(_temp.Path);

        result.Should().BeEmpty("file already present and md5 matches → no download → empty version");
        _mock.Server.LogEntries
            .Should().NotContain(e => e.RequestMessage.AbsolutePath.StartsWith("/files/"),
                "no binary download should occur when md5 matches");
    }

    [Fact]
    public void UpdateFirmware_RedownloadsAndDeletesOldFiles_WhenLocalMd5Mismatches()
    {
        var oldBytes = Encoding.UTF8.GetBytes("OLD_FIRMWARE");
        var newBytes = Encoding.UTF8.GetBytes("NEW_FIRMWARE");
        var newMd5 = Md5Hex(newBytes);
        string oldFilename = "pocket_firmware_v1_0.bin";
        string newFilename = "pocket_firmware_v2_0.bin";
        File.WriteAllBytes(Path.Combine(_temp.Path, oldFilename), oldBytes);
        string downloadUrl = _mock.BaseUrl + "/files/" + newFilename;

        _mock.Server
            .Given(Request.Create().WithPath("/support/pocket/firmware/latest/details").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody($$"""{ "download_url": "{{downloadUrl}}", "md5": "{{newMd5}}" }"""));

        _mock.Server
            .Given(Request.Create().WithPath("/files/" + newFilename).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(newBytes));

        var svc = new FirmwareService();
        string result = svc.UpdateFirmware(_temp.Path);

        result.Should().Be(newFilename);
        File.Exists(Path.Combine(_temp.Path, newFilename)).Should().BeTrue();
        File.Exists(Path.Combine(_temp.Path, oldFilename)).Should().BeFalse(
            "old firmware file matching pocket_firmware_*.bin pattern should be deleted");
    }
}
