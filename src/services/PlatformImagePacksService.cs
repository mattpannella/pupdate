using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Github;
using File = System.IO.File;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "<Pending>")]
public class PlatformImagePacksService : Base
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/image_packs.json";

    private readonly bool useLocalImagePacks;

    public string InstallPath { get; set; }
    public string GithubToken { get; set; }

    private List<PlatformImagePack> list;

    public List<PlatformImagePack> List
    {
        get
        {
            if (this.list == null)
            {
#if DEBUG
                string json = File.ReadAllText("image_packs.json");
#else
                string json = this.useLocalImagePacks
                    ? File.ReadAllText("image_packs.json")
                    : HttpHelper.Instance.GetHTML(END_POINT);
#endif
                this.list = JsonSerializer.Deserialize<List<PlatformImagePack>>(json);
            }

            return list;
        }
    }

    public PlatformImagePacksService(string path, string githubToken = null, bool useLocalImagePacks = false)
    {
        this.InstallPath = path;
        this.GithubToken = githubToken;
        this.useLocalImagePacks = useLocalImagePacks;
    }

    public void Install(string owner, string repository, string variant)
    {
        string localFile = Path.Combine(this.InstallPath, "image_pack.zip");
        Release release = GithubApiService.GetLatestRelease(owner, repository, this.GithubToken);

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

            HttpHelper.Instance.DownloadFile(downloadUrl, localFile);

            WriteMessage("Download complete.");
        }

        WriteMessage("Installing...");

        string extractPath = Path.Combine(this.InstallPath, "temp");

        ZipFile.ExtractToDirectory(localFile, extractPath, true);

        string imagePack = FindPlatformImagePack(extractPath);
        string target = Path.Combine(this.InstallPath, "Platforms", "_images");

        Util.CopyDirectory(imagePack, target, false, true);
        Directory.Delete(extractPath, true);
        File.Delete(localFile);

        WriteMessage("All Done");
    }

    private static string FindPlatformImagePack(string temp)
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
