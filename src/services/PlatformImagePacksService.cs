using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Github;
using File = System.IO.File;

namespace Pannella.Services;

public class PlatformImagePacksService : Base
{
    private const string END_POINT = "https://raw.githubusercontent.com/mattpannella/pupdate/main/image_packs.json";

    private readonly bool useLocalImagePacks;
    private readonly string installPath;
    private readonly string githubToken;

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
                this.list = JsonConvert.DeserializeObject<List<PlatformImagePack>>(json);
            }

            return list;
        }
    }

    public PlatformImagePacksService(string path, string githubToken = null, bool useLocalImagePacks = false)
    {
        this.installPath = path;
        this.githubToken = githubToken;
        this.useLocalImagePacks = useLocalImagePacks;
    }

    public void Install(string owner, string repository, string variant)
    {
        string localFile = Path.Combine(ServiceHelper.TempDirectory, "image_pack.zip");
        Release release = GithubApiService.GetLatestRelease(owner, repository, this.githubToken);

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

        string extractPath = Path.Combine(ServiceHelper.TempDirectory, "temp");
        ZipHelper.ExtractToDirectory(localFile, extractPath, true);

        string imagePack = FindPlatformImagePack(extractPath);
        string target = Path.Combine(this.installPath, "Platforms", "_images");

        Util.CopyDirectory(imagePack, target, false, true);
        Directory.Delete(extractPath, true);
        File.Delete(localFile);

        WriteMessage("Installation complete.");
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
