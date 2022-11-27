namespace pannella.analoguepocket;
using System.IO.Compression;

public class ImagePack
{
    public string owner { get; set; }
    public string repository { get; set; }
    public string? variant { get; set; }


    public async Task<bool> Install(string path)
    {
        string filepath = await fetchImagePack(path);
        await installImagePack(path, filepath);
        return true;
    }

    private async Task<string> fetchImagePack(string path)
    {
        Github.Release release = await GithubApi.GetLatestRelease(this.owner, this.repository);
        string localFile = Path.Combine(path, "imagepack.zip");
        string downloadUrl = "";
        if(release.assets == null) {
            throw new Exception("Github Release contains no assets");
        }
        if(this.variant == null) {
            downloadUrl = release.assets[0].browser_download_url;
        } else {
            foreach(Github.Asset asset in release.assets) {
                if(asset.name.Contains(this.variant)) {
                    downloadUrl = asset.browser_download_url;
                }
            }
        }
        if(downloadUrl != "") {
            Console.WriteLine("Downloading image pack...");
            await HttpHelper.DownloadFileAsync(downloadUrl, localFile);
            Console.WriteLine("Download complete.");
            return localFile;
        }
        return "";
    }

    private async Task installImagePack(string path, string filepath)
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

    private string FindImagePack(string temp)
    {
        string path = Path.Combine(temp, "Platforms", "_images");
        if(Directory.Exists(path)) {
            return path;
        }

        foreach(string d in Directory.EnumerateDirectories(temp)) {
            path = Path.Combine(d, "Platforms", "_images");
            if(Directory.Exists(path)) {
                return path;
            }
        }
        throw new Exception("Can't find image pack");
    }
}