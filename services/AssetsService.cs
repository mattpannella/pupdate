using System.Text.Json;

namespace pannella.analoguepocket;

public static class AssetsService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/main/pocket_updater_assets.json";
    private const string IMAGE_PACKS = "https://raw.githubusercontent.com/mattpannella/pocket-updater-utility/develop/image_packs.json";

    public static async Task<Dictionary<string, Dependency>> GetAssets()
    {
        string json = await HttpHelper.GetHTML(END_POINT);
        Dictionary<string, Dependency>? assets = JsonSerializer.Deserialize<Dictionary<string, Dependency>?>(json);

        if(assets != null) {
            return assets;
        }

        return new Dictionary<string, Dependency>();
    }

    public static async Task<ImagePack[]> GetImagePacks()
    {
        string json = await HttpHelper.GetHTML(IMAGE_PACKS);
        ImagePack[] packs = JsonSerializer.Deserialize<ImagePack[]?>(json);

        if(packs != null) {
            return packs;
        }

        return new ImagePack[0];
    }
}