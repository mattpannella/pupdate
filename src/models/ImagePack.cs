using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models.Github;
using Pannella.Services;
using File = System.IO.File;

namespace Pannella.Models;

public class ImagePack
{
    public string owner { get; set; }
    public string repository { get; set; }
    public string variant { get; set; }

    public async Task Install(string path)
    {
        string filepath = await this.FetchImagePack(path);

        await InstallImagePack(path, filepath);
    }

    private async Task<string> FetchImagePack(string path)
    {
        Release release = await GithubApiService.GetLatestRelease(this.owner, this.repository);
        string localFile = Path.Combine(path, "imagepack.zip");
        string downloadUrl = string.Empty;

        if (release.assets == null)
        {
            throw new Exception("Github Release contains no assets");
        }

        if (this.variant == null)
        {
            downloadUrl = release.assets[0].browser_download_url;
        }
        else
        {
            foreach (var asset in release.assets.Where(asset => asset.name.Contains(this.variant)))
            {
                downloadUrl = asset.browser_download_url;
            }
        }

        if (downloadUrl != string.Empty)
        {
            Console.WriteLine("Downloading image pack...");

            await HttpHelper.Instance.DownloadFileAsync(downloadUrl, localFile);

            Console.WriteLine("Download complete.");

            return localFile;
        }

        return string.Empty;
    }

    private static async Task InstallImagePack(string path, string filepath)
    {
        Console.WriteLine("Installing...");

        string extractPath = Path.Combine(path, "temp");

        ZipFile.ExtractToDirectory(filepath, extractPath, true);

        string imagePack = FindImagePack(extractPath);
        string target = Path.Combine(path, "Platforms", "_images");

        Util.CopyDirectory(imagePack, target, false, true);
        Directory.Delete(extractPath, true);
        File.Delete(filepath);

        Console.WriteLine("All Done");
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
