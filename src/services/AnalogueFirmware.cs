using System.Text.Json;

namespace pannella.analoguepocket;

public static class AnalogueFirmware
{
    private const string BASE_URL = "https://www.analogue.co/";
    private const string VERSION = "support/pocket/firmware/latest";
    private const string DOWNLOAD = "support/pocket/firmware/{0}/download";


    public static async Task<string> GetLatestVersion()
    {
        string url = BASE_URL + VERSION;
        string response = await Factory.GetHttpHelper().GetHTML(url, false);

        return response;
    }

    public static string GetFirmwareUrl(string version = "latest")
    {
        return String.Format(BASE_URL + DOWNLOAD, version);
    }
}