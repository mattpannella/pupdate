using System.Text.Json;

namespace pannella.analoguepocket;
using Analogue;

public static class AnalogueFirmware
{
    private const string BASE_URL = "https://www.analogue.co/";
    private const string VERSION = "support/pocket/firmware/latest";
    private const string DOWNLOAD = "support/pocket/firmware/{0}/download";
    private const string DETAILS = "support/pocket/firmware/{0}/details";

    private static ReleaseDetails latest = null;

    public static async Task<string> GetLatestVersion()
    {
        ReleaseDetails details = await GetDetails();
        return details.version;
    }

    public static async Task<ReleaseDetails> GetDetails(string version = "latest")
    {
        if(latest != null) {
            return latest;
        }
        string url = String.Format(BASE_URL + DETAILS, version);
        string response = await Factory.GetHttpHelper().GetHTML(url);
        ReleaseDetails details = JsonSerializer.Deserialize<ReleaseDetails>(response);

        if (version == "latest") {
            latest = details;
        }

        return details;
    }

    public static async Task<string> GetFirmwareUrl(string version = "latest")
    {
        ReleaseDetails details = await GetDetails(version);
        return details.download_url;
    }
}