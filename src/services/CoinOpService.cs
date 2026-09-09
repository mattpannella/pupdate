using System.Net;
using System.Net.Http.Headers;

namespace Pannella.Services;

public static class CoinOpService
{
    private const string LICENSE_ENDPOINT = "https://lic.coinopcollection.org/api/public/key/{0}";

    public static byte[] FetchLicense(string serial)
    {
        var client = new HttpClient();

        string url = string.Format(LICENSE_ENDPOINT, serial);
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        var agent = new ProductInfoHeaderValue("Pupdate", "1.0");

        request.Headers.UserAgent.Add(agent);

        var response = client.Send(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var responseBody = response.Content.ReadAsStringAsync().Result;
            throw new Exception(responseBody);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception("Error fetching Coin-Op Collection license.");
        }

        var bytes = response.Content.ReadAsByteArrayAsync().Result;

        return bytes;
    }
}
