using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Models.Extras;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public class PocketExtrasService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/blacklist.json";

    public static async Task<List<PocketExtra>> GetPocketExtrasList()
    {
#if DEBUG
        string json = await File.ReadAllTextAsync("pocket_extras.json");
#else
        string json = await HttpHelper.Instance.GetHTML(END_POINT);
#endif
        PocketExtras files = JsonSerializer.Deserialize<PocketExtras>(json);

        return files.pocket_extras;
    }
}
