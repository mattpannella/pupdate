using Pannella.Helpers;
using GithubFile = Pannella.Models.Github.File;

namespace Pannella.Services;

public partial class CoresService
{
    private const string JTBETA_KEY_FILENAME = "jtbeta.zip";
    private const string JTBETA_KEY_ALT_FILENAME = "beta.bin";
    private const string JOTEGO_PATREON_VANITY = "jotego";

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
    private void AutoFetchJtBetaFromPatreon()
    {
        string cookie = ServiceHelper.SettingsService.Config.patreon_session_cookie;

        if (string.IsNullOrWhiteSpace(cookie))
        {
            Console.WriteLine("JT Beta auto-fetch is enabled but no Patreon session cookie is set.");
            Console.WriteLine("Set it via: Pocket Setup > Set Patreon Session Cookie.");
            Console.WriteLine("Skipping auto-fetch. Falling back to manual key if present.");
            Divide();
            return;
        }

        try
        {
            WriteMessage("Attempting to auto-fetch Jotego jtbeta.zip from Patreon...");

            byte[] zipBytes = PatreonService.FetchAttachment(
                cookie, JOTEGO_PATREON_VANITY, JTBETA_KEY_FILENAME, out string sourcePostUrl);
            string destinationZip = Path.Combine(this.installPath, JTBETA_KEY_FILENAME);

            File.WriteAllBytes(destinationZip, zipBytes);

            WriteMessage($"Downloaded jtbeta.zip from {sourcePostUrl}. Extracting...");

            // Re-run extraction so the standard code path handles it.
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
