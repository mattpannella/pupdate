using System.Net.Http.Headers;
using Newtonsoft.Json;
using Pannella.Models.Github;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella.Services;

public static class GithubApiService
{
    private const string RELEASES = "https://api.github.com/repos/{0}/{1}/releases";
    private const string CONTENTS = "https://api.github.com/repos/{0}/{1}/contents/{2}";

    public static int RemainingCalls { get; private set; } = 60; // default without a token

    public static List<Release> GetReleases(string user, string repository, string githubToken = null)
    {
        string url = string.Format(RELEASES, user, repository);
        var responseBody = CallApi(url, githubToken);
        List<Release> releases = JsonConvert.DeserializeObject<List<Release>>(responseBody) ?? new List<Release>();

        return releases;
    }

    public static Release GetRelease(string user, string repository, string tagName, string githubToken = null)
    {
        string url = string.Format(RELEASES, user, repository) + "/tags/" + tagName;
        var responseBody = CallApi(url, githubToken);
        Release release = JsonConvert.DeserializeObject<Release>(responseBody);

        return release;
    }

    public static Release GetLatestRelease(string user, string repository, string githubToken = null)
    {
        string url = string.Format(RELEASES, user, repository) + "/latest";
        var responseBody = CallApi(url, githubToken);
        Release release = JsonConvert.DeserializeObject<Release>(responseBody);

        return release;
    }

    public static GithubFile GetFile(string user, string repository, string path, string githubToken = null)
    {
        string url = string.Format(CONTENTS, user, repository, path);
        var responseBody = CallApi(url, githubToken);
        GithubFile file = JsonConvert.DeserializeObject<GithubFile>(responseBody);

        return file;
    }

    public static List<GithubFile> GetFiles(string user, string repository, string path, string githubToken = null)
    {
        string url = string.Format(CONTENTS, user, repository, path);
        var responseBody = CallApi(url, githubToken);
        List<GithubFile> files = JsonConvert.DeserializeObject<List<GithubFile>>(responseBody);

        return files;
    }

    private static string CallApi(string url, string token)
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

        if (response.Headers.TryGetValues("x-ratelimit-remaining", out var values))
        {
            string remaining = values.First();

            RemainingCalls = int.Parse(remaining);
        }

        var responseBody = response.Content.ReadAsStringAsync().Result;

        return responseBody;
    }
}
