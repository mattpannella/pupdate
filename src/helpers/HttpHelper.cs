using System.Net;

namespace Pannella.Helpers;

public class HttpHelper
{
    private static HttpHelper instance;
    private static readonly object SYNC_LOCK = new();
    private HttpClient client;

    public event EventHandler<DownloadProgressEventArgs> DownloadProgressUpdate;

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
                return instance ??= new HttpHelper();
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
        this.client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = allowRedirect });
        this.client.Timeout = TimeSpan.FromMinutes(10); // 10min
    }

    private void OnDownloadProgressUpdate(DownloadProgressEventArgs e)
    {
        EventHandler<DownloadProgressEventArgs> handler = DownloadProgressUpdate;

        handler?.Invoke(this, e);
    }
}

public class DownloadProgressEventArgs : EventArgs
{
    public double Progress;
}
