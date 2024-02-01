using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;

namespace Pannella.Services;

public static class ImagePacksService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/image_packs.json";

    public static async Task<ImagePack[]> GetImagePacks()
    {
        string json = await HttpHelper.Instance.GetHTML(END_POINT);
        ImagePack[] packs = JsonSerializer.Deserialize<ImagePack[]>(json);

        return packs ?? Array.Empty<ImagePack>();
    }
}
