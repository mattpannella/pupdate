using pannella.analoguepocket;
using System.Runtime.InteropServices;
using CommandLine;
using System.IO.Compression;

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
            bool forceUpdate = false;

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
                    if(o.ForceUpdate) {
                        forceUpdate = true;
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
                    PauseExit();
                }
            }

            PocketCoreUpdater updater = new PocketCoreUpdater(path);
            SettingsManager settings = new SettingsManager(path);

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

            if(coreSelector || settings.GetConfig().core_selector) {
                List<Core> cores =  await CoresService.GetCores();
                RunCoreSelector(settings, cores);
            }

            if(!forceUpdate) {
                bool flag = true;
                while(flag) {
                    int choice = DisplayMenu();

                    switch(choice) {
                        case 1:
                            await updater.UpdateFirmware();
                            Pause();
                            break;
                        case 2:
                            List<Core> cores =  await CoresService.GetCores();
                            RunCoreSelector(settings, cores);
                            Pause();
                            break;
                        case 3:
                            await ImagePackSelector(path);
                            break;
                        case 4:
                            flag = false;
                            break;
                        case 0:
                        default:
                            Console.WriteLine("Starting update process...");
                            await updater.RunUpdates();
                            Pause();
                            break;
                    }
                }
            } else {
                Console.WriteLine("Starting update process...");
                await updater.RunUpdates();
                Pause();
            }
        } catch (Exception e) {
            Console.WriteLine("Well, something went wrong. Sorry about that.");
            Console.WriteLine(e.Message);
            Pause();
        }
    }

    static void RunCoreSelector(SettingsManager settings, List<Core> cores)
    {
        ConsoleKey response;
        Console.WriteLine("\nSelect your cores! The available cores will be listed 1 at a time. For each one, hit 'n' if you don't want it installed, or just hit enter if you want it. Ok you've got this. Here we go...\n");
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

    private static int DisplayMenu()
    {
        Console.Clear();
        string welcome = @"
 __          __  _                            _          ______ _                         _______                  
 \ \        / / | |                          | |        |  ____| |                       |__   __|                 
  \ \  /\  / /__| | ___ ___  _ __ ___   ___  | |_ ___   | |__  | | __ ___   _____  _ __     | | _____      ___ __  
   \ \/  \/ / _ \ |/ __/ _ \| '_ ` _ \ / _ \ | __/ _ \  |  __| | |/ _` \ \ / / _ \| '__|    | |/ _ \ \ /\ / / '_ \ 
    \  /\  /  __/ | (_| (_) | | | | | |  __/ | || (_) | | |    | | (_| |\ V / (_) | |       | | (_) \ V  V /| | | |
     \/  \/ \___|_|\___\___/|_| |_| |_|\___|  \__\___/  |_|    |_|\__,_| \_/ \___/|_|       |_|\___/ \_/\_/ |_| |_|
                                                                                                                   
                                                                                                                   
                                                                                                                   ";
        Console.WriteLine(welcome);
        
        foreach(var(item, index) in menuItems.WithIndex()) {
            Console.WriteLine($"{index}) {item}");
        }
        Console.Write("\nChoose your destiny: ");
        int choice;
        bool result = int.TryParse(Console.ReadLine(), out choice);
        if (result) {
            return choice;
        }
        return 0;
    }

    private static async Task ImagePackSelector(string path)
    {
        Console.Clear();
        Console.WriteLine("Checking for image packs...\n");
        ImagePack[] packs = await ImagePacksService.GetImagePacks(); 
        if(packs.Length > 0) {
            foreach(var(pack, index) in packs.WithIndex()) {
                Console.WriteLine($"{index}) {pack.owner}: {pack.repository} {pack.variant}");
            }
            Console.WriteLine($"{packs.Length}) Go back");
            Console.Write("\nSo, what'll it be?: ");
            int choice;
            bool result = int.TryParse(Console.ReadLine(), out choice);
            if (result && choice < packs.Length && choice >= 0) {
                await InstallImagePack(path, packs[choice]);
                Pause();
            } else if(choice == packs.Length) {
                return;
            } else {
                Console.WriteLine("you fucked up");
                Pause();
            }
        } else {
            Console.WriteLine("None found. Have a nice day");
            Pause();
        }
    }

    private static async Task InstallImagePack(string path, ImagePack pack)
    {
        pack.Install(path);
        //string filepath = await fetchImagePack(path, pack);
        //await installImagePack(path, filepath);
    }

    private static void PauseExit()
    {
        Console.ReadLine(); //wait for input so the console doesn't auto close in windows
        Environment.Exit(1);
    }

    private static void Pause()
    {
        Console.ReadLine();
    }

    private static string[] menuItems = {
        "Update All",
        "Update Firmware",
        "Select Cores",
        "Download Image Packs",
        "Exit"
    };
}

public class Options
{
    [Option('u', "update", HelpText = "Force updater to just run update process, instead of displaying the menu.", Required = false)]
    public bool ForceUpdate { get; set; }
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

    [Option ('a', "all", Required = false, HelpText = "Extract all release assets, instead of just ones containing openFPGA cores.")]
    public bool ExtractAll { get; set; }

    [Option ('c', "coreselector", Required = false, HelpText = "Run the core selector.")]
    public bool CoreSelector { get; set; }

    [Option ('f', "platformsfolder", Required = false, HelpText = "Preserve the Platforms folder, so customizations aren't overwritten by updates.")]
    public bool PreservePlatformsFolder { get; set; }
}

public static class EnumExtension {
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)       
       => self.Select((item, index) => (item, index));
}