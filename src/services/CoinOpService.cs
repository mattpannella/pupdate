using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Pannella.Helpers;

namespace Pannella.Services;

public static class CoinOpService
{
    private const string BETA_ENDPOINT = "https://key.coinopcollection.org/?username={0}";

    public static string FetchBetaKey(string url)
    {
        var client = new HttpClient();
        /*
        https://key.coinopcollection.org/?username=develprx@gmail.com
passing case:
https://key.coinopcollection.org/?username=sctestuser@proton.me
*/
url = "https://key.coinopcollection.org/?username=sctestuser@proton.me";
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        var response = client.Send(request);

        if (response.StatusCode == HttpStatusCode.NotFound) {

        }

        var responseBody = response.Content.ReadAsStringAsync().Result;
        var bytes = response.Content.ReadAsByteArrayAsync().Result;
        File.WriteAllBytes(Path.Combine(ServiceHelper.UpdateDirectory, "coinop.key"), bytes);

        return responseBody;
    }
}
