namespace Pannella.Helpers;

public class HttpHelper
{
    private static HttpHelper instance;
    private static readonly object syncLock = new();
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
            lock (syncLock)
            {
                return instance ??= new HttpHelper();
            }
        }
    }

    public async Task DownloadFileAsync(string uri, string outputPath, int timeout = 100)
    {
        bool console = false;

        try
        {
            var test = Console.WindowWidth;

            console = true;
        }
        catch (Exception)
        {
            // Do Nothing.
        }

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("URI is invalid.");
        }

        using HttpResponseMessage r = await this.client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        var totalSize = r.Content.Headers.ContentLength ?? -1L;
        var readSoFar = 0L;
        var buffer = new byte[4096];
        var isMoreToRead = true;

        using var stream = await r.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        while (isMoreToRead)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);

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
                    var progressWidth = Console.WindowWidth - 14;
                    var progressBarWidth = (int)(progress * progressWidth);
                    var progressBar = new string('=', progressBarWidth);
                    var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);

                    Console.Write($"\r{progressBar}{emptyProgressBar}] {(progress * 100):0.00}%");

                    if (readSoFar == totalSize)
                    {
                        Console.CursorLeft = 0;
                        Console.Write(new string(' ', Console.WindowWidth));
                        Console.CursorLeft = 0;
                    }
                }

                DownloadProgressEventArgs args = new()
                {
                    Progress = progress
                };

                OnDownloadProgressUpdate(args);

                await fileStream.WriteAsync(buffer, 0, read);
            }
        }
    }

    public async Task<string> GetHTML(string uri, bool allowRedirect = true)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("URI is invalid.");
        }

        if (!allowRedirect)
        {
            this.CreateClient(false);
        }

        var response = await this.client.GetAsync(uri);
        string html = await response.Content.ReadAsStringAsync();

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
