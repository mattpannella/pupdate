using Pannella.Helpers;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella.Services;

public partial class CoresService
{
    private const string JTBETA_KEY_FILENAME = "jtbeta.zip";
    private const string JTBETA_KEY_ALT_FILENAME = "beta.bin";
    private const string JOTEGO_PATREON_VANITY = "jotego";
    private const string JTBETA_GITHUB_OWNER = "jotego";
    private const string JTBETA_GITHUB_REPO = "jtbeta";
    private const string JTBETA_GITHUB_PATH = "jtbeta.zip";

    private Dictionary<string, string> renamedPlatformFiles;

    public Dictionary<string, string> RenamedPlatformFiles
    {
        get { return renamedPlatformFiles ??= this.LoadRenamedPlatformFiles(); }
    }

    private Dictionary<string, string> LoadRenamedPlatformFiles()
    {
        Dictionary<string, string> platformFiles = new();

        try
        {
            List<GithubFile> files = GithubApiService.GetFiles("dyreschlock", "pocket-platform-images",
                "arcade/Platforms", this.settingsService.Config.github_token);
            //grab the home platforms, too, to make sure neogeo pocket gets updated
            files.AddRange(GithubApiService.GetFiles("dyreschlock", "pocket-platform-images",
                "home/Platforms", this.settingsService.Config.github_token));

            foreach (var file in files)
            {
                string url = file.download_url;
                string filename = file.name;

                if (filename.EndsWith(".json"))
                {
                    string platform = Path.GetFileNameWithoutExtension(filename);

                    platformFiles.Add(platform, url);
                }
            }
        }
        catch (Exception ex)
        {
            WriteMessage("Unable to retrieve archive contents. Asset download may not work.");
            WriteMessage(this.settingsService.Debug.show_stack_traces
                ? ex.ToString()
                : Util.GetExceptionMessage(ex));
        }

        return platformFiles;
    }

    // ReSharper disable once InconsistentNaming
    private bool ExtractJTBetaKey()
    {
        string keyPath = Path.Combine(this.installPath, LICENSE_EXTRACT_LOCATION);
        string zipFile = Path.Combine(this.installPath, JTBETA_KEY_FILENAME);

        if (File.Exists(zipFile))
        {
            WriteMessage("JT beta key detected. Extracting...");
            ZipHelper.ExtractToDirectory(zipFile, keyPath, true, false);

            return true;
        }

        string binFile = Path.Combine(this.installPath, JTBETA_KEY_ALT_FILENAME);

        if (File.Exists(binFile))
        {
            WriteMessage("JT beta key detected.");

            if (!Directory.Exists(keyPath))
            {
                Directory.CreateDirectory(keyPath);
            }

            File.Copy(binFile, Path.Combine(keyPath, JTBETA_KEY_ALT_FILENAME), true);

            return true;
        }

        return false;
    }

    // ReSharper disable once InconsistentNaming
    private void AutoFetchJtBetaKey()
    {
        var config = ServiceHelper.SettingsService.Config;

        // Try GitHub first — stable auth, official API, long-lived tokens.
        if (config.jt_beta_github_fetch && TryFetchJtBetaFromGithub())
        {
            return;
        }

        // Fall back to Patreon cookie scraping.
        if (config.jt_beta_patreon_fetch)
        {
            TryFetchJtBetaFromPatreon();
        }
    }

    private bool TryFetchJtBetaFromGithub()
    {
        string token = ServiceHelper.SettingsService.Config.github_token;

        if (string.IsNullOrWhiteSpace(token))
        {
            WriteMessage("JT Beta GitHub fetch is enabled but no github_token is set.");
            WriteMessage("Add one to pupdate_settings.json and make sure your GitHub account has access to " +
                         $"{JTBETA_GITHUB_OWNER}/{JTBETA_GITHUB_REPO} (granted via Patreon link on GitHub).");
            Divide();
            return false;
        }

        try
        {
            WriteMessage($"Attempting to fetch jtbeta.zip from GitHub ({JTBETA_GITHUB_OWNER}/{JTBETA_GITHUB_REPO})...");

            byte[] zipBytes = GithubApiService.DownloadFileContents(
                JTBETA_GITHUB_OWNER, JTBETA_GITHUB_REPO, JTBETA_GITHUB_PATH, token);
            string destinationZip = Path.Combine(this.installPath, JTBETA_KEY_FILENAME);

            File.WriteAllBytes(destinationZip, zipBytes);

            WriteMessage($"Downloaded jtbeta.zip ({zipBytes.Length:N0} bytes) from GitHub. Extracting...");

            this.ExtractJTBetaKey();
            Divide();
            return true;
        }
        catch (Exception ex)
        {
            WriteMessage("GitHub fetch failed: " + ex.Message);
            WriteMessage("Your github_token may lack access to the repo, or the repo layout may have changed.");
            Divide();
            return false;
        }
    }

    private void TryFetchJtBetaFromPatreon()
    {
        string cookie = ServiceHelper.SettingsService.Config.patreon_session_cookie;

        if (string.IsNullOrWhiteSpace(cookie))
        {
            WriteMessage("JT Beta Patreon fetch is enabled but no Patreon session cookie is set.");
            WriteMessage("Set it via: Pocket Setup > Set Patreon Session Cookie.");
            Divide();
            return;
        }

        try
        {
            WriteMessage("Attempting to fetch jtbeta.zip from Patreon...");

            byte[] zipBytes = PatreonService.FetchAttachment(
                cookie, JOTEGO_PATREON_VANITY, JTBETA_KEY_FILENAME, out string sourcePostUrl);
            string destinationZip = Path.Combine(this.installPath, JTBETA_KEY_FILENAME);

            File.WriteAllBytes(destinationZip, zipBytes);

            WriteMessage($"Downloaded jtbeta.zip from {sourcePostUrl}. Extracting...");

            this.ExtractJTBetaKey();
        }
        catch (Exception ex)
        {
            WriteMessage("Patreon auto-fetch failed: " + ex.Message);
            WriteMessage("Session cookie may be expired, or your subscription tier " +
                         "doesn't include beta access. Continuing with update.");
        }
        finally
        {
            Divide();
        }
    }
}
