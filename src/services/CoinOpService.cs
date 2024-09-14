using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Pannella.Helpers;

namespace Pannella.Services;

public static class CoinOpService
{
    private const string BETA_ENDPOINT = "https://key.coinopcollection.org/?username={0}";

    public static byte[] FetchBetaKey(string email)
    {
        var client = new HttpClient();

        string url = String.Format(BETA_ENDPOINT, email);
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        var response = client.Send(request);

        if (response.StatusCode == HttpStatusCode.NotFound) {
            var responseBody = response.Content.ReadAsStringAsync().Result;
            throw new Exception(responseBody);
        } else if (response.StatusCode != HttpStatusCode.OK) {
            throw new Exception("Didn't work");
        }

        var bytes = response.Content.ReadAsByteArrayAsync().Result;

        return bytes;
    }
}
