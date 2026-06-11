using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace Pannella.Helpers;

public class HttpHelper
{
    private static HttpHelper INSTANCE;
    private static readonly object SYNC_LOCK = new();
    private HttpClient client;

    private HttpClientHandler handler;

    public event EventHandler<DownloadProgressEventArgs> DownloadProgressUpdate;

    private bool loggedIn = false;

    // Files smaller than this aren't worth the per-chunk request overhead.
    private const long CHUNK_MIN_SIZE = 1 * 1024 * 1024;

    public bool ConcurrentDownloadsEnabled { get; set; } = false;
    public int DownloadChunkCount { get; set; } = 4;

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
        bool console = ConsoleIsAvailable();

        using var cts = new CancellationTokenSource();

        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("URI is invalid.");
        }

        if (this.ConcurrentDownloadsEnabled && this.DownloadChunkCount > 1)
        {
            (bool supported, long totalSize) = TryGetRangeSupport(uri, cts.Token);

            if (supported && totalSize >= CHUNK_MIN_SIZE)
            {
                try
                {
                    DownloadChunked(uri, outputPath, totalSize, this.DownloadChunkCount, console, cts.Token);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Any chunked failure (mid-stream drop, unexpected status, etc.) falls
                    // back to the plain sequential download below before surfacing an error.
                }
            }
        }

        DownloadSequential(uri, outputPath, console, cts.Token);
    }

    private void DownloadSequential(string uri, string outputPath, bool console, CancellationToken token)
    {
        using HttpResponseMessage responseMessage = this.client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).Result;

        // Just in case the HttpClient doesn't throw the error on 404 like it should.
        if (responseMessage.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException("Not Found.", null, HttpStatusCode.NotFound);
        }

        var totalSize = responseMessage.Content.Headers.ContentLength ?? -1L;
        var readSoFar = 0L;
        var buffer = new byte[4096];
        var isMoreToRead = true;
        var stopwatch = Stopwatch.StartNew();

        using var stream = responseMessage.Content.ReadAsStream(token);
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
                    double seconds = stopwatch.Elapsed.TotalSeconds;
                    double speed = seconds > 0 ? readSoFar / seconds : 0;

                    ConsoleHelper.ShowProgressBar(readSoFar, totalSize, speed);
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

    // Probes for HTTP range support with a 1-byte request. A 206 response with a
    // Content-Range total length means we can split the download into chunks.
    private (bool supported, long totalSize) TryGetRangeSupport(string uri, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            request.Headers.Range = new RangeHeaderValue(0, 0);

            using HttpResponseMessage response =
                this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).Result;

            // Just in case the HttpClient doesn't throw the error on 404 like it should.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new HttpRequestException("Not Found.", null, HttpStatusCode.NotFound);
            }

            if (response.StatusCode == HttpStatusCode.PartialContent &&
                response.Content.Headers.ContentRange is { HasLength: true, Length: > 0 } contentRange)
            {
                return (true, contentRange.Length.Value);
            }

            return (false, -1L);
        }
        catch (HttpRequestException)
        {
            // Preserve the 404 (and similar) behavior callers rely on.
            throw;
        }
        catch
        {
            // Anything else (e.g. server rejects the probe) just means no chunking.
            return (false, -1L);
        }
    }

    private void DownloadChunked(string uri, string outputPath, long totalSize, int chunkCount, bool console,
        CancellationToken token)
    {
        // Pre-create and size the output file so each chunk can write to its own offset.
        using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            fileStream.SetLength(totalSize);
        }

        long chunkSize = totalSize / chunkCount;
        long totalRead = 0;
        var progressLock = new object();
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < chunkCount; i++)
        {
            long start = i * chunkSize;
            long end = (i == chunkCount - 1) ? totalSize - 1 : start + chunkSize - 1;

            tasks.Add(Task.Run(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);

                request.Headers.Range = new RangeHeaderValue(start, end);

                using HttpResponseMessage response =
                    await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    throw new HttpRequestException(
                        $"Expected 206 Partial Content but got {(int)response.StatusCode}.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.ReadWrite);

                fileStream.Seek(start, SeekOrigin.Begin);

                var buffer = new byte[81920];
                int read;

                while ((read = await stream.ReadAsync(buffer, token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), token);

                    long soFar = Interlocked.Add(ref totalRead, read);

                    lock (progressLock)
                    {
                        if (console)
                        {
                            double seconds = stopwatch.Elapsed.TotalSeconds;
                            double speed = seconds > 0 ? soFar / seconds : 0;

                            ConsoleHelper.ShowProgressBar(soFar, totalSize, speed);
                        }

                        OnDownloadProgressUpdate(new DownloadProgressEventArgs
                        {
                            Progress = (double)soFar / totalSize
                        });
                    }
                }
            }, token));
        }

        Task.WhenAll(tasks).GetAwaiter().GetResult();

        if (console)
        {
            Console.Write("\r");
        }
    }

    private static bool ConsoleIsAvailable()
    {
        try
        {
            _ = Console.WindowWidth;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void GetAuthCookie(string username, string password, string loginUrl, Dictionary<string, string> additional)
    {
        //this code is all internet archive specific now, but whatever
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
