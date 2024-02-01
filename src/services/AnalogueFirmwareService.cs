using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Analogue;

namespace Pannella.Services;

public static class AnalogueFirmwareService
{
    private const string BASE_URL = "https://www.analogue.co/";
    private const string DETAILS = "support/pocket/firmware/{0}/details";

    private static ReleaseDetails latest;

    public static async Task<ReleaseDetails> GetDetails(string version = "latest")
    {
        if (latest != null)
        {
            return latest;
        }

        string url = string.Format(BASE_URL + DETAILS, version);
        string response = await HttpHelper.Instance.GetHTML(url);
        ReleaseDetails details = JsonSerializer.Deserialize<ReleaseDetails>(response);

        if (version == "latest")
        {
            latest = details;
        }

        return details;
    }
}
