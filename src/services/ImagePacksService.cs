using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public static class ImagePacksService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/image_packs.json";

    public static async Task<ImagePack[]> GetImagePacks()
    {
#if DEBUG
        string json = await File.ReadAllTextAsync("image_packs.json");
#else
        string json = GlobalHelper.SettingsManager.GetConfig().use_local_image_packs
            ? await File.ReadAllTextAsync("image_packs.json")
            : await HttpHelper.Instance.GetHTML(END_POINT);
#endif
        ImagePack[] packs = JsonSerializer.Deserialize<ImagePack[]>(json);

        return packs ?? Array.Empty<ImagePack>();
    }
}
