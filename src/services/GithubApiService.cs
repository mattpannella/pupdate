using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Github;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public static class GithubApiService
{
    private const string RELEASES = "https://api.github.com/repos/{0}/{1}/releases";
    private const string CONTENTS = "https://api.github.com/repos/{0}/{1}/contents/{2}";

    private static int remainingCalls = 60; //default without a token

    public static List<Release> GetReleases(string user, string repository)
    {
        string url = string.Format(RELEASES, user, repository);
        var responseBody = CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        List<Release> releases = JsonSerializer.Deserialize<List<Release>>(responseBody) ?? new List<Release>();

        return releases;
    }

    public static Release GetRelease(string user, string repository, string tagName)
    {
        string url = string.Format(RELEASES, user, repository) + "/tags/" + tagName;
        var responseBody = CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        Release release = JsonSerializer.Deserialize<Release>(responseBody);

        return release;
    }

    public static Release GetLatestRelease(string user, string repository)
    {
        string url = string.Format(RELEASES, user, repository) + "/latest";
        var responseBody = CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        Release release = JsonSerializer.Deserialize<Release>(responseBody);

        return release;
    }

    public static GithubFile GetFile(string user, string repository, string path)
    {
        string url = string.Format(CONTENTS, user, repository, path);
        var responseBody = CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        GithubFile file = JsonSerializer.Deserialize<GithubFile>(responseBody);

        return file;
    }

    public static List<GithubFile> GetFiles(string user, string repository, string path)
    {
        string url = string.Format(CONTENTS, user, repository, path);
        var responseBody = CallAPI(url, GlobalHelper.SettingsManager.GetConfig().github_token);
        List<GithubFile> files = JsonSerializer.Deserialize<List<GithubFile>>(responseBody);

        return files;
    }

    private static string CallAPI(string url, string token)
    {
        var client = new HttpClient();

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };

        var agent = new ProductInfoHeaderValue("Pupdate", "1.0");

        request.Headers.UserAgent.Add(agent);

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        }

        var response = client.Send(request);

        response.EnsureSuccessStatusCode();

        if(response.Headers.TryGetValues("x-ratelimit-remaining", out var values))
        {
            string remaining = values.First();
            remainingCalls = int.Parse(remaining);
        }

        var responseBody = response.Content.ReadAsStringAsync().Result;

        return responseBody;
    }

    public static int GetRateLimitRemaining()
    {
        return remainingCalls;
    }
}
