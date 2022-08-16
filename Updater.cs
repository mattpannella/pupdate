using System;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers; 

public class Updater
{
    private const string baseDir = "/Users/matt/development/c#/pocket_updater/";

    //private const string semverRegex = "^[0-9]+\.[0-9]+\.[0-9]+$";

    private const string semverFinder = @"\D*(\d(\.\d)*\.\d)\D*";

    const string apiUrl = "https://api.github.com/repos/{0}/{1}/releases";
    const string jsonFile = baseDir + "auto_update.json";
    public static readonly string[] ZIP_TYPES = {"application/x-zip-compressed", "application/zip"};

    public async Task runUpdates()
    {
        string json = File.ReadAllText(jsonFile);
        List<Core>? coresList = JsonSerializer.Deserialize<List<Core>>(json);
        //TODO check if null
        foreach(Core core in coresList) {
            Repo repo = core.repo;
            string name = core.name;
            bool allowPrerelease = core.allowPrerelease;
            string url = String.Format(apiUrl, repo.user, repo.project);
            string response = await fetchReleases(url);
            List<Github.Release>? releases = JsonSerializer.Deserialize<List<Github.Release>>(response);
            var mostRecentRelease = getMostRecentRelease(releases, allowPrerelease);
            
            string tag_name = mostRecentRelease.tag_name;
            List<Github.Asset> assets = mostRecentRelease.assets;

            Regex r = new Regex(semverFinder);
            Match matches = r.Match(tag_name);

            var releaseSemver = matches.Groups[1].Value;
            //TODO throw some error if it doesn't find a semver in the tag
            releaseSemver = this.semverFix(releaseSemver);

            Github.Asset coreAsset = null;

            // might need to search for the right zip here if there's more than one
            //iterate through assets to find the zip release
            for(int i = 0; i < assets.Count; i++) {
                if(ZIP_TYPES.Contains(assets[i].content_type)) {
                    coreAsset = assets[i];
                    break;
                }
            }

            if(coreAsset == null) {
                Console.WriteLine("No zip file found for release. Skipping");
                continue;
            }

            string nameGuess = name ?? coreAsset.name.Split("_")[0];

            bool fileExists = File.Exists(baseDir + "Cores/"+nameGuess+"/core.json");

              if (fileExists){
                /*
                const localData = await fs.readJson(`./Cores/${nameGuess}/core.json`);
                let ver_string = localData.core.metadata.version;
                
                matches = ver_string.match(semverFinder);
                if(matches && matches.length > 1) {
                var localSemver = matches[1];
                localSemver = semverFix(localSemver);
                } else {
                var localSemver = "";
                }

                console.log(chalk.yellow(`Local core found: v${localSemver}`));

                if (!isActuallySemver(localSemver) || !isActuallySemver(releaseSemver)){
                console.log(chalk.red("Code not semver'd, downloading just incase..."));
                // could compare release dates here but you'd miss any releases made within 1 day
                updateCore(coreAsset.browser_download_url);
                continue
                }

                if (semverCompare(releaseSemver, localSemver)){
                updateCore(coreAsset.browser_download_url);
                }else{
                console.log(chalk.yellow(`Up to date, skipping core.`));
                }
                */
            } else {
                await updateCore(coreAsset.browser_download_url);
            }
        }
    }

    private Github.Release getMostRecentRelease(List<Github.Release> releases, bool allowPrerelease)
    {
        foreach(Github.Release release in releases) {
            if(!release.draft && (allowPrerelease || !release.prerelease)) {
                return release;
            }
        }

        return null;
    }

    private async Task<string> fetchReleases(string url)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };
        var agent = new ProductInfoHeaderValue("Analogue-Pocket-Auto-Updater", "1.0");
        request.Headers.UserAgent.Add(agent);
        var response = await client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return responseBody;
    }

    //even though its technically not a valid semver, allow use of 2 part versions, and just add a .0 to complete the 3rd part
    public string semverFix(string version)
    {
        string[] parts = version.Split(".");

        if(parts.Length == 2) {
            version += ".0";
        }
        
        return version;
    }

    public bool semverCompare(string semverA, string semverB)
    {
        Version verA = Version.Parse(semverA);
        Version verB = Version.Parse(semverB);
        
        switch(verA.CompareTo(verB))
        {
            case 0:
            case -1:
                return false;
            case 1:
                return true;
            default:
                return true;
        }
    }

    private bool isActuallySemver(string potentiallySemver)
    {
        Version ver = null;
        return Version.TryParse(potentiallySemver, out ver);
    }

    public async Task updateCore(string downloadLink)
    {
        string zipPath = baseDir + "testing.zip";
        string extractPath = baseDir + "testing-zip";
        await HttpHelper.DownloadFileAsync(downloadLink, zipPath);

        ZipFile.ExtractToDirectory(zipPath, extractPath, true);

        File.Delete(zipPath);
    }
}