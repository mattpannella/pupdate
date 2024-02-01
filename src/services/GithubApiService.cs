using System.Net.Http.Headers;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Github;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella.Services;

public static class GithubApiService
{
    private const string RELEASES = "https://api.github.com/repos/{0}/{1}/releases";
    private const string CONTENTS = "https://api.github.com/repos/{0}/{1}/contents/{2}";

    public static async Task<List<Release>> GetReleases(string user, string repository)
    {
        string url = string.Format(RELEASES, user, repository);
        var responseBody = await CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        List<Release> releases = JsonSerializer.Deserialize<List<Release>>(responseBody) ?? new List<Release>();

        return releases;
    }

    public static async Task<Release> GetRelease(string user, string repository, string tagName)
    {
        string url = string.Format(RELEASES, user, repository) + "/tags/" + tagName;
        var responseBody = await CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        Release release = JsonSerializer.Deserialize<Release>(responseBody);

        return release;
    }

    public static async Task<Release> GetLatestRelease(string user, string repository)
    {
        string url = string.Format(RELEASES, user, repository) + "/latest";
        var responseBody = await CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        Release release = JsonSerializer.Deserialize<Release>(responseBody);

        return release;
    }

    public static async Task<GithubFile> GetFile(string user, string repository, string path)
    {
        string url = string.Format(CONTENTS, user, repository, path);
        var responseBody = await CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        GithubFile file = JsonSerializer.Deserialize<GithubFile>(responseBody);

        return file;
    }

    public static async Task<List<GithubFile>> GetFiles(string user, string repository, string path)
    {
        string url = string.Format(CONTENTS, user, repository, path);
        var responseBody = await CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        List<GithubFile> files = JsonSerializer.Deserialize<List<GithubFile>>(responseBody);

        return files;
    }

    private static async Task<string> CallAPI(string url, string token)
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

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        }

        var response = await client.SendAsync(request).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return responseBody;
    }
}
