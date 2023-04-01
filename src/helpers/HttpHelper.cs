using System.IO;
using System.Net.Http;

namespace pannella.analoguepocket;

public class HttpHelper
{
    private static HttpHelper instance = null;
    private static object syncLock = new object();
    private HttpClient client = null;

    private HttpHelper()
    {
        this.client = new HttpClient();
        this.client.Timeout = TimeSpan.FromMinutes(10); //10min
    }

    public static HttpHelper Instance
    {
        get
        {
            lock (syncLock)
            {
                if (HttpHelper.instance == null) {
                    HttpHelper.instance = new HttpHelper();
                }

                return HttpHelper.instance;
            }
        }
    }

   public async Task DownloadFileAsync(string uri, string outputPath, int timeout = 100)
   {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));
        Uri? uriResult;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out uriResult))
            throw new InvalidOperationException("URI is invalid.");

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
                Console.Write("\r");
            }
            else
            {
                readSoFar += read;
                var progress = (double)readSoFar / totalSize;
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
                await fileStream.WriteAsync(buffer, 0, read);
            }
        }
        //byte[] fileBytes = await r.Content.ReadAsByteArrayAsync();

        //byte[] fileBytes = await this.client.GetByteArrayAsync(uri, cts.Token);
        //File.WriteAllBytes(outputPath, fileBytes);
    }

   public async Task<String> GetHTML(string uri)
   {
        Uri? uriResult;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out uriResult))
            throw new InvalidOperationException("URI is invalid.");

        string html = await this.client.GetStringAsync(uri);

        return html;
   }
}
