using System.Text.Json;

namespace pannella.analoguepocket;
using archiveorg;

public static class ArchiveService
{
    private const string END_POINT = "https://archive.org/metadata/{0}";


    public static async Task<Archive> GetFiles(string archive)
    {
        string url = String.Format(END_POINT, archive);
        string json = await HttpHelper.GetHTML(url);
        Archive result = JsonSerializer.Deserialize<Archive>(json);

        return result;
    }
}