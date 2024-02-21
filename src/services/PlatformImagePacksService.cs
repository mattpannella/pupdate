using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Github;
using File = System.IO.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public class PlatformImagePacksService : BaseService
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/image_packs.json";

    public static async Task<List<ImagePack>> GetImagePacks()
    {
#if DEBUG
        string json = await File.ReadAllTextAsync("image_packs.json");
#else
        string json = GlobalHelper.SettingsManager.GetConfig().use_local_image_packs
            ? await File.ReadAllTextAsync("image_packs.json")
            : await HttpHelper.Instance.GetHTML(END_POINT);
#endif
        var packs = JsonSerializer.Deserialize<List<ImagePack>>(json);

        return packs ?? new List<ImagePack>();
    }

    public async Task Install(string path, string owner, string repository, string variant)
    {
        string filepath = await FetchImagePack(path, owner, repository, variant);

        InstallImagePack(path, filepath);
    }

    private async Task<string> FetchImagePack(string path, string owner, string repository, string variant)
    {
        Release release = await GithubApiService.GetLatestRelease(owner, repository);

        if (release.assets == null)
        {
            throw new Exception("Github Release contains no assets");
        }

        string downloadUrl = variant == null
            ? release.assets[0].browser_download_url
            : release.assets.Single(asset => asset.name.EndsWith($"{variant}.zip")).browser_download_url;

        if (downloadUrl != string.Empty)
        {
            WriteMessage("Downloading image pack...");

            string localFile = Path.Combine(path, "image_pack.zip");

            await HttpHelper.Instance.DownloadFileAsync(downloadUrl, localFile);

            WriteMessage("Download complete.");

            return localFile;
        }

        return string.Empty;
    }

    private void InstallImagePack(string path, string filepath)
    {
        WriteMessage("Installing...");

        string extractPath = Path.Combine(path, "temp");

        ZipFile.ExtractToDirectory(filepath, extractPath, true);

        string imagePack = FindImagePack(extractPath);
        string target = Path.Combine(path, "Platforms", "_images");

        Util.CopyDirectory(imagePack, target, false, true);
        Directory.Delete(extractPath, true);
        File.Delete(filepath);

        WriteMessage("All Done");
    }

    private static string FindImagePack(string temp)
    {
        string path = Path.Combine(temp, "Platforms", "_images");

        if (Directory.Exists(path))
        {
            return path;
        }

        foreach (string d in Directory.EnumerateDirectories(temp))
        {
            path = Path.Combine(d, "Platforms", "_images");

            if (Directory.Exists(path))
            {
                return path;
            }
        }

        throw new Exception("Can't find image pack");
    }
}
