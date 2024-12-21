using System.Net;
using System.Net.Cache;
using System.Net.Http.Headers;
using System.Web;

namespace Pannella.Helpers;

public class HttpHelper
{
    private static HttpHelper instance;
    private static readonly object SYNC_LOCK = new();
    private HttpClient client;

    private HttpClientHandler handler;

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

    public void GetAuthCookie(string username, string password, string loginUrl, Dictionary<string, string> additional)
    {
        var loginUri = new Uri(loginUrl);
        var host = loginUri.GetLeftPart(UriPartial.Authority);
        var cookies = this.handler.CookieContainer.GetCookies(new Uri(host));
        if(cookies.Count() > 0) {
            return;
        }
        var data = new List<KeyValuePair<string, string>>();
        data.Add(new KeyValuePair<string, string>("username", username));
        data.Add(new KeyValuePair<string, string>("password", password) );

        foreach (var item in additional)
        {
            data.Add(new KeyValuePair<string, string>(item.Key, item.Value));
        }
        var formData = new FormUrlEncodedContent(data);
        //throwing shit at the wall at this point
        this.client.DefaultRequestHeaders.Add("User-Agent", "Pupdate");
        HttpResponseMessage loginResponse = this.client.PostAsync(loginUrl, formData).Result;
        if (loginResponse.IsSuccessStatusCode)
        {
            //do one more?
            loginResponse = this.client.PostAsync(loginUrl, formData).Result;
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
        //Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36
        this.handler = new HttpClientHandler { AllowAutoRedirect = allowRedirect, CookieContainer = new CookieContainer() };
        this.client = new HttpClient(this.handler);
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
