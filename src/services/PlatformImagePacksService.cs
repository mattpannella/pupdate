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

    public static List<PlatformImagePack> GetPlatformImagePacks()
    {
#if DEBUG
        string json = File.ReadAllText("image_packs.json");
#else
        string json = GlobalHelper.SettingsManager.GetConfig().use_local_image_packs
            ? File.ReadAllText("image_packs.json")
            : HttpHelper.Instance.GetHTML(END_POINT);
#endif
        var packs = JsonSerializer.Deserialize<List<PlatformImagePack>>(json);

        return packs ?? new List<PlatformImagePack>();
    }

    public void Install(string path, string owner, string repository, string variant)
    {
        string filepath = FetchImagePack(path, owner, repository, variant);

        InstallImagePack(path, filepath);
    }

    private string FetchImagePack(string path, string owner, string repository, string variant)
    {
        Release release = GithubApiService.GetLatestRelease(owner, repository);

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

            HttpHelper.Instance.DownloadFile(downloadUrl, localFile);

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
