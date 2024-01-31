using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http;

namespace pannella.analoguepocket;

public static class GithubApi
{
    private const string RELEASES = "https://api.github.com/repos/{0}/{1}/releases";
    private const string CONTENTS = "https://api.github.com/repos/{0}/{1}/contents/{2}";

    public static async Task<List<Github.Release>> GetReleases(string user, string repository, string token = "")
    {
        string url = String.Format(RELEASES, user, repository);
        var responseBody = await CallAPI(url, token);

        List<Github.Release> releases = JsonSerializer.Deserialize<List<Github.Release>>(responseBody);

        if(releases == null) {
            releases = new List<Github.Release>();
        }

        return releases;
    }

    public static async Task<Github.Release> GetRelease(string user, string repository, string tag_name, string token = "")
    {
        string url = String.Format(RELEASES, user, repository) + "/tags/" + tag_name;
        
        var responseBody = await CallAPI(url, token);
        Github.Release release = JsonSerializer.Deserialize<Github.Release>(responseBody);
        
        return release;
    }

    public static async Task<Github.Release> GetLatestRelease(string user, string repository, string token = "")
    {
        string url = String.Format(RELEASES, user, repository) + "/latest";
        
        var responseBody = await CallAPI(url, token);
        Github.Release release = JsonSerializer.Deserialize<Github.Release>(responseBody);
        
        return release;
    }

    public static async Task<Github.File> GetFile(string user, string repository, string path, string token = "")
    {
        string url = String.Format(CONTENTS, user, repository, path);
        
        var responseBody = await CallAPI(url, token);
        Github.File file = JsonSerializer.Deserialize<Github.File>(responseBody);
        
        return file;
    }

    public static async Task<List<Github.File>> GetFiles(string user, string repository, string path, string token = "")
    {
        string url = String.Format(CONTENTS, user, repository, path);
        
        var responseBody = await CallAPI(url, token);
        List<Github.File> files = JsonSerializer.Deserialize<List<Github.File>>(responseBody);
        
        return files;
    }

    private static async Task<string> CallAPI(string url, string token = "")
    {
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

        return responseBody;
    }
}
