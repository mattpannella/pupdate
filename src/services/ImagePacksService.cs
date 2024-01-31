using System.Text.Json;

namespace pannella.analoguepocket;

public static class ImagePacksService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/main/image_packs.json";

    public static async Task<ImagePack[]> GetImagePacks()
    {
        string json = await Factory.GetHttpHelper().GetHTML(END_POINT);
        ImagePack[] packs = JsonSerializer.Deserialize<ImagePack[]>(json);

        if(packs != null) {
            return packs;
        }

        return new ImagePack[0];
    }
}