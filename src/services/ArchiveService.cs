using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Archive;

namespace Pannella.Services;

public static class ArchiveService
{
    private const string END_POINT = "https://archive.org/metadata/{0}";

    public static async Task<Archive> GetFiles(string archive)
    {
        string url = string.Format(END_POINT, archive);
        string json = await HttpHelper.Instance.GetHTML(url);
        Archive result = JsonSerializer.Deserialize<Archive>(json);

        return result;
    }

    public static async Task<Archive> GetFilesCustom(string url)
    {
        try
        {
            string json = await HttpHelper.Instance.GetHTML(url);
            Archive result = JsonSerializer.Deserialize<Archive>(json);

            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
