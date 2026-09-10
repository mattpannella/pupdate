using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace Pannella.Services;

public static class CoinOpService
{
    private const string LICENSE_ENDPOINT = "https://lic.coinopcollection.org/api/public/key/{0}";

    public static byte[] FetchLicense(string serial)
    {
        string url = string.Format(LICENSE_ENDPOINT, Uri.EscapeDataString(serial));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Pupdate", "1.0"));

        using var response = client.Send(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Exception(GetErrorMessage(response) ??
                $"No Coin-Op Collection license found for serial '{serial}'. " +
                "Check the serial on the Coin-Op license portal.");
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception(GetErrorMessage(response) ??
                $"Error fetching Coin-Op Collection license ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        var bytes = response.Content.ReadAsByteArrayAsync().Result;

        if (bytes.Length == 0)
        {
            throw new Exception("The Coin-Op Collection license response was empty.");
        }

        return bytes;
    }

    private static string GetErrorMessage(HttpResponseMessage response)
    {
        try
        {
            string body = response.Content.ReadAsStringAsync().Result;

            if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith("{"))
            {
                return null;
            }

            string message = JObject.Parse(body)["error"]?.ToString();

            return string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        }
        catch
        {
            return null;
        }
    }
}
