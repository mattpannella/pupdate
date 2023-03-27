using System.Text.Json;

namespace pannella.analoguepocket;

public static class AssetsService
{
    private const string IMAGE_PACKS = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/main/image_packs.json";
    private const string BLACKLIST = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/main/blacklist.json";

    public static async Task<ImagePack[]> GetImagePacks()
    {
        string json = await Factory.GetHttpHelper().GetHTML(IMAGE_PACKS);
        ImagePack[] packs = JsonSerializer.Deserialize<ImagePack[]?>(json);

        if(packs != null) {
            return packs;
        }

        return new ImagePack[0];
    }

    public static async Task<string[]> GetBlacklist()
    {
        string json = await Factory.GetHttpHelper().GetHTML(BLACKLIST);
        string[] files = JsonSerializer.Deserialize<string[]?>(json);

        if(files != null) {
            return files;
        }

        return new string[0];
    }
}