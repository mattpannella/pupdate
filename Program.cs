using pannella.analoguepocket;
using System.Runtime.InteropServices;
using CommandLine;

internal class Program
{
    private static string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    private const string USER = "mattpannella";
    private const string REPOSITORY = "pocket-updater-utility";
    private const string RELEASE_URL = "https://github.com/mattpannella/pocket-updater-utility/releases/download/{0}/pocket_updater_{1}.zip";
    private static async Task Main(string[] args)
    {
        try {
            string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string? path = Path.GetDirectoryName(location);
            bool extractAll = false;
            bool coreSelector = false;
            bool preservePlatformsFolder = false;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    if(o.InstallPath != null && o.InstallPath != "") {
                        Console.WriteLine("path: " + o.InstallPath);
                        path = o.InstallPath;
                    }
                    if(o.ExtractAll) {
                        extractAll = true;
                    }
                    if(o.CoreSelector) {
                        coreSelector = true;
                    }
                    if(o.PreservePlatformsFolder) {
                        preservePlatformsFolder = true;
                    }
                }
                ).WithNotParsed<Options>(o => 
                    {
                        if(o.IsHelp()) {
                            Environment.Exit(1);
                        }
                        if(o.IsVersion()) {
                            Environment.Exit(1);
                        }
                    }
                );
            
            //path = "/Users/mattpannella/pocket-test";

            ConsoleKey response;

            Console.WriteLine("Analogue Pocket Core Updater v" + version);
            Console.WriteLine("Checking for updates...");
            if(await CheckVersion(path)) {
                Console.WriteLine("Would you like to continue anyway? [Y/n]:");
                response = Console.ReadKey(false).Key;
                if (response == ConsoleKey.N) {
                    Console.WriteLine("Come again soon");
                    Console.ReadLine(); //wait for input so the console doesn't auto close in windows
                    Environment.Exit(1);
                }
            }

            PocketCoreUpdater updater = new PocketCoreUpdater(path);
            SettingsManager settings = new SettingsManager(path);

            if(coreSelector || settings.GetConfig().core_selector) {
                List<Core> cores =  await CoresService.GetCores();
                RunCoreSelector(settings, cores);
            }

            if(preservePlatformsFolder || settings.GetConfig().preserve_platforms_folder) {
                updater.PreservePlatformsFolder(true);
            }

            updater.ExtractAll(extractAll);
            updater.DeleteSkippedCores(settings.GetConfig().delete_skipped_cores);
            updater.SetGithubApiKey(settings.GetConfig().github_token);
            updater.DownloadFirmware(settings.GetConfig().download_firmware);
            updater.StatusUpdated += updater_StatusUpdated;
            updater.UpdateProcessComplete += updater_UpdateProcessComplete;
            updater.DownloadAssets(settings.GetConfig().download_assets);
            await updater.Initialize();

            Console.WriteLine("Starting update process...");

            await updater.RunUpdates();
        } catch (Exception e) {
            Console.WriteLine("Well, something went wrong. Sorry about that.");
            Console.WriteLine(e.Message);
        }
        
        Console.ReadLine(); //wait for input so the console doesn't auto close in windows
    }

    static void RunCoreSelector(SettingsManager settings, List<Core> cores)
    {
        ConsoleKey response;
        Console.WriteLine("Select your cores! The available cores will be listed 1 at a time. For each one, hit 'n' if you don't want it installed, or just hit enter if you want it. Ok you've got this. Here we go...");
        foreach(Core core in cores) {
            Console.Write(core.identifier + "?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if (response == ConsoleKey.N) {
                settings.DisableCore(core.identifier);
            } else {
                settings.EnableCore(core.identifier);
            }
            Console.WriteLine("");
        }
        settings.GetConfig().core_selector = false;
        settings.SaveSettings();
    }

    static void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    static void updater_UpdateProcessComplete(object sender, UpdateProcessCompleteEventArgs e)
    {
        Console.WriteLine("-------------");
        Console.WriteLine(e.Message);
        if(e.InstalledCores.Count > 0) {
            Console.WriteLine("Cores Updated:");
            foreach(Dictionary<string, string> core in e.InstalledCores) {
                Console.WriteLine(core["core"] + " " + core["version"]);
            }
            Console.WriteLine("");
        }
        if(e.InstalledAssets.Count > 0) {
            Console.WriteLine("Assets Installed:");
            foreach(string asset in e.InstalledAssets) {
                Console.WriteLine(asset);
            }
            Console.WriteLine("");
        }
        if(e.FirmwareUpdated != "") {
            Console.WriteLine("New Firmware was downloaded. Restart your Pocket to install");
            Console.WriteLine(e.FirmwareUpdated);
            Console.WriteLine("");
        }
        Console.WriteLine("we did it, come again soon");
    }

    //return true if newer version is available
    async static Task<bool> CheckVersion(string path)
    {
        try {
            List<Github.Release> releases = await GithubApi.GetReleases(USER, REPOSITORY);

            string tag_name = releases[0].tag_name;
            string? v = SemverUtil.FindSemver(tag_name);
            if(v != null) {
                bool check = SemverUtil.SemverCompare(v, version);
                if(check) {
                    Console.WriteLine("A new version is available. Downloading now...");
                    string platform = GetPlatform();
                    string url = String.Format(RELEASE_URL, tag_name, platform);
                    string saveLocation = Path.Combine(path, "pocket_updater.zip");
                    await HttpHelper.DownloadFileAsync(url, saveLocation);
                    Console.WriteLine("Download complete.");
                    Console.WriteLine(saveLocation);
                    Console.WriteLine("Go to " + releases[0].html_url + " for a change log");
                }
                return check;
            }

            return false;
        } catch (HttpRequestException e) {
            return false;
        }
    }

    private static string GetPlatform()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return "win";
        }
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return "mac";
        }
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return "linux";
        }

        return "";
    }
}

public class Options
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

    [Option ('a', "all", Required = false, HelpText = "Extract all release assets, instead of just ones containing openFPGA cores.")]
    public bool ExtractAll { get; set; }

    [Option ('c', "coreselector", Required = false, HelpText = "Run the core selector.")]
    public bool CoreSelector { get; set; }

    [Option ('f', "platformsfolder", Required = false, HelpText = "Preserve the Platforms folder, so customizations aren't overwritten by updates.")]
    public bool PreservePlatformsFolder { get; set; }
}