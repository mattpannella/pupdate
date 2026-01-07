using System.Net;

namespace Pannella.Helpers;

public class HttpHelper
{
    private static HttpHelper INSTANCE;
    private static readonly object SYNC_LOCK = new();
    private HttpClient client;

    private HttpClientHandler handler;

    public event EventHandler<DownloadProgressEventArgs> DownloadProgressUpdate;

    private bool loggedIn = false;

    private HttpHelper()
    {
        this.CreateClient();
    }

    public static HttpHelper Instance
    {
        get
        {
            lock (SYNC_LOCK)
            {
                return INSTANCE ??= new HttpHelper();
            }
        }
    }

    public void DownloadFile(string uri, string outputPath, int timeout = 100)
    {
        bool console = false;

        try
        {
            _ = Console.WindowWidth;
            console = true;
        }
        catch
        {
            // Ignore
        }

        using var cts = new CancellationTokenSource();

        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("URI is invalid.");
        }

        using HttpResponseMessage responseMessage = this.client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token).Result;

        // Just in case the HttpClient doesn't throw the error on 404 like it should.
        if (responseMessage.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException("Not Found.", null, HttpStatusCode.NotFound);
        }

        var totalSize = responseMessage.Content.Headers.ContentLength ?? -1L;
        var readSoFar = 0L;
        var buffer = new byte[4096];
        var isMoreToRead = true;

        using var stream = responseMessage.Content.ReadAsStream(cts.Token);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        while (isMoreToRead)
        {
            var read = stream.Read(buffer);

            if (read == 0)
            {
                isMoreToRead = false;

                if (console)
                {
                    Console.Write("\r");
                }
            }
            else
            {
                readSoFar += read;

                var progress = (double)readSoFar / totalSize;

                if (console)
                {
                    ConsoleHelper.ShowProgressBar(readSoFar, totalSize);
                }

                DownloadProgressEventArgs args = new()
                {
                    Progress = progress
                };

                OnDownloadProgressUpdate(args);

                fileStream.Write(buffer, 0, read);
            }
        }
    }

    public void GetAuthCookie(string username, string password, string loginUrl, Dictionary<string, string> additional)
    {
        if (this.loggedIn)
            return;

        var archiveUri = new Uri("https://archive.org");
        
        // First, GET the login page to establish session
        var loginPageUrl = "https://archive.org/account/login";
        this.client.GetAsync(loginPageUrl).Wait();
        
        // Second, GET the token from the AJAX endpoint
        var tokenResponse = this.client.GetAsync(loginUrl).Result;
        var tokenJson = tokenResponse.Content.ReadAsStringAsync().Result;
        
        // Parse the JSON to extract the token
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(tokenJson, @"""token""\s*:\s*""([^""]+)""");
        if (!tokenMatch.Success)
        {
            throw new Exception("Failed to extract authentication token from Archive.org");
        }
        
        var token = tokenMatch.Groups[1].Value;

        var jsonData = new
        {
            username = username,
            password = password,
            remember = true,
            t = token
        };

        var jsonString = System.Text.Json.JsonSerializer.Serialize(jsonData);
        var jsonContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

        this.client.DefaultRequestHeaders.Clear();
        this.client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        this.client.DefaultRequestHeaders.Add("Accept", "*/*");
        this.client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        this.client.DefaultRequestHeaders.Add("Referer", loginPageUrl);
        this.client.DefaultRequestHeaders.Add("Origin", "https://archive.org");
        this.client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        this.client.DefaultRequestHeaders.Add("Pragma", "no-cache");

        HttpResponseMessage loginResponse = this.client.PostAsync(loginUrl, jsonContent).Result;
        
        if (!loginResponse.IsSuccessStatusCode)
        {
            var errorBody = loginResponse.Content.ReadAsStringAsync().Result;
            throw new Exception($"Archive.org login failed: {errorBody}");
        }
        
        // Verify we got the auth cookies
        var cookies = this.handler.CookieContainer.GetCookies(archiveUri);
        bool hasAuthCookie = cookies.Cast<Cookie>().Any(c => 
            c.Name == "logged-in-user" || c.Name == "logged-in-sig");
        
        if (!hasAuthCookie)
        {
            throw new Exception("Archive.org login succeeded but authentication cookies were not set");
        }
        
        this.loggedIn = true;
    }

    // ReSharper disable once InconsistentNaming
    public string GetHTML(string uri, bool allowRedirect = true)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("URI is invalid.");
        }

        if (!allowRedirect)
        {
            this.CreateClient(false);
        }
        
        var response = this.client.GetAsync(uri).Result;

        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        string html = response.StatusCode switch
        {
            HttpStatusCode.OK => response.Content.ReadAsStringAsync().Result,
            _ => throw new HttpRequestException($"{response.StatusCode}: {uri}", null, response.StatusCode)
        };

        if (!allowRedirect)
        {
            this.CreateClient();
        }

        return html;
    }

    private void CreateClient(bool allowRedirect = true)
    {
        // Preserve existing cookies when recreating client
        var existingCookies = this.handler?.CookieContainer;
        
        this.handler = new HttpClientHandler 
        { 
            AllowAutoRedirect = allowRedirect,
            UseCookies = true,
            CookieContainer = existingCookies ?? new CookieContainer()
        };
        this.client = new HttpClient(this.handler);
        this.client.Timeout = TimeSpan.FromMinutes(10); // 10min
    }


    private void OnDownloadProgressUpdate(DownloadProgressEventArgs e)
    {
        EventHandler<DownloadProgressEventArgs> downloadProgressUpdateHandler = DownloadProgressUpdate;

        downloadProgressUpdateHandler?.Invoke(this, e);
    }
}

public class DownloadProgressEventArgs : EventArgs
{
    public double Progress;
}
