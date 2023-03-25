using System.IO;
using System.Net.Http;

namespace pannella.analoguepocket;

public static class HttpHelper
{
   private static readonly HttpClient _httpClient = new HttpClient();

   public static async Task DownloadFileAsync(string uri, string outputPath, int timeout = 100)
   {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));
        Uri? uriResult;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out uriResult))
            throw new InvalidOperationException("URI is invalid.");

        byte[] fileBytes = await _httpClient.GetByteArrayAsync(uri, cts.Token);
        File.WriteAllBytes(outputPath, fileBytes);
    }

   public static async Task<String> GetHTML(string uri)
   {
        Uri? uriResult;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out uriResult))
            throw new InvalidOperationException("URI is invalid.");

        string html = await _httpClient.GetStringAsync(uri);

        return html;
   }
}
