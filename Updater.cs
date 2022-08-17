using System;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers; 

public class Updater
{
    private string baseDir;

    private const string semverFinder = @"\D*(\d(\.\d)*\.\d)\D*";

    private const string apiUrl = "https://api.github.com/repos/{0}/{1}/releases";
    private string jsonFile;
    private static readonly string[] ZIP_TYPES = {"application/x-zip-compressed", "application/zip"};

    public Updater(string coresFile, string baseDirectory)
    {
        //make sure the path ends in a /
        if(!baseDirectory.EndsWith(Path.DirectorySeparatorChar)) {
            baseDirectory += Path.DirectorySeparatorChar;
        }
        this.baseDir = baseDirectory;

        //make sure the json file exists
        if(File.Exists(coresFile)) {
            this.jsonFile = coresFile;
        } else {
            throw new FileNotFoundException("Cores json file not found!");
        }
    }

    public async Task runUpdates()
    {
        string json = File.ReadAllText(jsonFile);
        List<Core>? coresList = JsonSerializer.Deserialize<List<Core>>(json);
        //TODO check if null
        foreach(Core core in coresList) {
            Repo repo = core.repo;
            Console.WriteLine("Starting Repo: " + repo.project);
            string name = core.name;
            bool allowPrerelease = core.allowPrerelease;
            string url = String.Format(apiUrl, repo.user, repo.project);
            string response = await fetchReleases(url);
            if(response == null) {
                Environment.Exit(1);
            }
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
            Console.WriteLine(tag_name + " is the most recent release, checking local core...");
            string localCoreFile = baseDir + "Cores/"+nameGuess+"/core.json";
            bool fileExists = File.Exists(localCoreFile);

              if (fileExists) {
                json = File.ReadAllText(localCoreFile);
                
                Analogue.Config? config = JsonSerializer.Deserialize<Analogue.Config>(json);
                Analogue.Core localCore = config.core;
                string ver_string = localCore.metadata.version;

                matches = r.Match(ver_string);
                string localSemver = "";
                if(matches != null && matches.Groups.Count > 1) {
                    localSemver = matches.Groups[1].Value;
                    localSemver = semverFix(localSemver);
                    Console.WriteLine("local core found: v" + localSemver);
                }

                if (!isActuallySemver(localSemver) || !isActuallySemver(releaseSemver)) {
                    Console.WriteLine("downloading core anyway");
                    await updateCore(coreAsset.browser_download_url);
                    continue;
                }

                if (semverCompare(releaseSemver, localSemver)){
                    Console.WriteLine("Updating core");
                    await updateCore(coreAsset.browser_download_url);
                } else {
                    Console.WriteLine("Up to date. Skipping core");
                }
            } else {
                Console.WriteLine("Downloading core");
                await updateCore(coreAsset.browser_download_url);
            }
            Console.WriteLine("------------");
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
        try {
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
        } catch (HttpRequestException e) {
            Console.WriteLine("Error pulling communicating with Github API.");
            Console.WriteLine(e.Message);
            return null;
        }
    }

    //even though its technically not a valid semver, allow use of 2 part versions, and just add a .0 to complete the 3rd part
    private string semverFix(string version)
    {
        string[] parts = version.Split(".");

        if(parts.Length == 2) {
            version += ".0";
        }
        
        return version;
    }

    private bool semverCompare(string semverA, string semverB)
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

    private async Task updateCore(string downloadLink)
    {
        Console.WriteLine("Downloading file " + downloadLink + "...");
        string zipPath = baseDir + "core.zip";
        string extractPath = baseDir;
        await HttpHelper.DownloadFileAsync(downloadLink, zipPath);

        Console.WriteLine("Extracting...");
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);
        File.Delete(zipPath);
        Console.WriteLine("Installation complete.");
    }
}