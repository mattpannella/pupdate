using System.Net;
using System.Net.Http.Headers;

namespace Pannella.Services;

public static class CoinOpService
{
    private const string LICENSE_ENDPOINT = "https://key.coinopcollection.org/?username={0}";

    public static byte[] FetchLicense(string email)
    {
        var client = new HttpClient();

        string url = String.Format(LICENSE_ENDPOINT, System.Web.HttpUtility.UrlEncode(email));
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        var agent = new ProductInfoHeaderValue("Pupdate", "1.0");

        request.Headers.UserAgent.Add(agent);

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
