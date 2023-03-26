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

        byte[] fileBytes = await this.client.GetByteArrayAsync(uri, cts.Token);
        File.WriteAllBytes(outputPath, fileBytes);
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
