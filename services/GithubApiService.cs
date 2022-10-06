using System.Text.Json;
using System.Net.Http.Headers;

namespace pannella.analoguepocket;

public static class GithubApi
{
    private const string END_POINT = "https://api.github.com/repos/{0}/{1}/releases";

    public static async Task<List<Github.Release>> GetReleases(string user, string repository, string? token = "")
    {
        string url = String.Format(END_POINT, user, repository);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };
        var agent = new ProductInfoHeaderValue("Analogue-Pocket-Updater-Utility", "1.0");
        request.Headers.UserAgent.Add(agent);
        if(token != null && token != "") {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        }
        var response = await client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        List<Github.Release>? releases = JsonSerializer.Deserialize<List<Github.Release>>(responseBody);

        if(releases == null) {
            releases = new List<Github.Release>();
        }

        return releases;
    }
}