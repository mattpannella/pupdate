using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using CommandLine;
using pannella.analoguepocket;

internal class Program
{
    private static string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    private const string USER = "mattpannella";
    private const string REPOSITORY = "pocket-updater-utility";
    private const string RELEASE_URL = "https://github.com/mattpannella/pocket-updater-utility/releases/download/{0}/pocket_updater_{1}.zip";

    private static bool cliMode = false;
    private static async Task Main(string[] args)
    {
        try {
            string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string? path = Path.GetDirectoryName(location);
            bool coreSelector = false;
            bool preservePlatformsFolder = false;
            bool forceUpdate = false;
            bool forceInstanceGenerator = false;


            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    if(o.InstallPath != null && o.InstallPath != "") {
                        Console.WriteLine("path: " + o.InstallPath);
                        path = o.InstallPath;
                        cliMode = true;
                    }
                    if(o.CoreSelector) {
                        coreSelector = true;
                        cliMode = true;
                    }
                    if(o.PreservePlatformsFolder) {
                        preservePlatformsFolder = true;
                        cliMode = true;
                    }
                    if(o.ForceUpdate) {
                        forceUpdate = true;
                        cliMode = true;
                    }
                    if(o.ForceInstanceGenerator) {
                        forceInstanceGenerator = true;
                        cliMode = true;
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
                string platform = GetPlatform();
                ConsoleKey[] acceptedInputs = new[] { ConsoleKey.I, ConsoleKey.C, ConsoleKey.Q };
                do {
                    if (platform == "win") {
                        Console.Write("Would you like to [i]nstall the update, [c]ontinue with the current version, or [q]uit? [i/c/q]: ");
                    } else {
                        Console.Write("Update downloaded and extracted. Would you like to [c]ontinue with the current version, or [q]uit? [c/q]: ");
                    }
                    response = Console.ReadKey(false).Key;
                    Console.WriteLine();
                } while(!acceptedInputs.Contains(response));

                switch(response) {
                    case ConsoleKey.I:
                        int result = UpdateSelfAndRun(path, args);
                        Environment.Exit(result);
                        break;

                    case ConsoleKey.C:
                        break;

                    case ConsoleKey.Q:
                        Console.WriteLine("Come again soon");
                        PauseExit();
                        break;
                }
            }

            PocketCoreUpdater updater = new PocketCoreUpdater(path);
            SettingsManager settings = new SettingsManager(path);

            if(preservePlatformsFolder || settings.GetConfig().preserve_platforms_folder) {
                updater.PreservePlatformsFolder(true);
            }

            updater.DeleteSkippedCores(settings.GetConfig().delete_skipped_cores);
            updater.SetGithubApiKey(settings.GetConfig().github_token);
            updater.DownloadFirmware(settings.GetConfig().download_firmware);
            updater.RenameJotegoCores(settings.GetConfig().fix_jt_names);
            updater.StatusUpdated += updater_StatusUpdated;
            updater.UpdateProcessComplete += updater_UpdateProcessComplete;
            updater.DownloadAssets(settings.GetConfig().download_assets);
            await updater.Initialize();

            if(coreSelector || settings.GetConfig().core_selector) {
                settings.EnableMissingCores(updater.GetMissingCores());
                List<Core> cores = await CoresService.GetCores();
                AskAboutNewCores(ref settings, true);
                RunCoreSelector(ref settings, cores);
                updater.LoadSettings();
            }

            // If we have any missing cores, handle them.
            if(updater.GetMissingCores().Any()) {
                Console.WriteLine("\nNew cores found since the last run.");
                AskAboutNewCores(ref settings);

                string? download_new_cores = settings.GetConfig().download_new_cores?.ToLowerInvariant();
                switch(download_new_cores) {
                    case "yes":
                        Console.WriteLine("The following cores have been enabled:");
                        foreach(Core core in updater.GetMissingCores())
                            Console.WriteLine($"- {core.identifier}");

                        settings.EnableMissingCores(updater.GetMissingCores());
                        settings.SaveSettings();
                        Console.WriteLine("\nPress ENTER to continue.");
                        Pause();
                        break;
                    case "no":
                        Console.WriteLine("The following cores have been disabled:");
                        foreach(Core core in updater.GetMissingCores())
                            Console.WriteLine($"- {core.identifier}");

                        settings.DisableMissingCores(updater.GetMissingCores());
                        settings.SaveSettings();
                        Console.WriteLine("\nPress ENTER to continue.");
                        Pause();
                        break;
                    default:
                    case "ask":
                        RunCoreSelector(ref settings, updater.GetMissingCores());
                        break;
                }

                updater.LoadSettings();
            }
            if(forceUpdate) {
                Console.WriteLine("Starting update process...");
                await updater.RunUpdates();
                Pause();
            } else if(forceInstanceGenerator) {
                await RunInstanceGenerator(updater, true);
            } else {
                bool flag = true;
                while(flag) {
                    int choice = DisplayMenu();

                    switch(choice) {
                        case 1:
                            await updater.UpdateFirmware();
                            Pause();
                            break;
                        case 2:
                            Console.WriteLine("Checking for requied files...");
                            await updater.RunAssetDownloader();
                            Pause();
                            break;
                        case 3:
                            List<Core> cores = await CoresService.GetCores();
                            AskAboutNewCores(ref settings, true);
                            RunCoreSelector(ref settings, cores);
                            updater.LoadSettings();

                            Console.WriteLine("\nDone!  Press ENTER to continue.");
                            Pause();
                            break;
                        case 4:
                            await ImagePackSelector(path);
                            break;
                        case 5:
                            await RunInstanceGenerator(updater);
                            Pause();
                            break;
                        case 6:
                            RunConfigWizard(ref settings);
                            SetUpdaterFlags(ref updater, ref settings);
                            Pause();
                            break;
                        case 7:
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
            }
        } catch(Exception e) {
            Console.WriteLine("Well, something went wrong. Sorry about that.");
            Console.WriteLine(e.Message);
            Pause();
        }
    }

    private static int UpdateSelfAndRun(string directory, string[] updaterArgs)
    {
        string execName = "pocket_updater";
        if(GetPlatform() == "win") {
            execName += ".exe";
        }
        string execLocation = Path.Combine(directory, execName);
        string backupName = $"{execName}.backup";
        string backupLocation = Path.Combine(directory, backupName);
        string updateName = "pocket_updater.zip";
        string updateLocation = Path.Combine(directory, updateName);

        int exitcode = int.MinValue;

        try {
            // Load System.IO.Compression now
            Assembly.Load("System.IO.Compression");

            // Move current process file
            Console.WriteLine($"Renaming {execLocation} to {backupLocation}");
            File.Move(execLocation, backupLocation, true);

            // Extract update
            Console.WriteLine($"Extracting {updateLocation} to {directory}");
            ZipFile.ExtractToDirectory(updateLocation, directory, true);

            // Execute
            Console.WriteLine($"Executing {execLocation}");
            ProcessStartInfo pInfo = new ProcessStartInfo(execLocation) {
                Arguments = string.Join(' ', updaterArgs),
                UseShellExecute = false
            };

            Process p = Process.Start(pInfo);
            p.WaitForExit();
            exitcode = p.ExitCode;
        } catch(Exception e) {
            Console.Error.WriteLine($"An error occurred: {e.GetType().Name}:{e.ToString()}");
        }

        return exitcode;
    }

    static void SetUpdaterFlags(ref PocketCoreUpdater updater, ref SettingsManager settings)
    {
        updater.DeleteSkippedCores(settings.GetConfig().delete_skipped_cores);
        updater.SetGithubApiKey(settings.GetConfig().github_token);
        updater.DownloadFirmware(settings.GetConfig().download_firmware);
        updater.DownloadAssets(settings.GetConfig().download_assets);
        updater.RenameJotegoCores(settings.GetConfig().fix_jt_names);
    }

    static async Task RunInstanceGenerator(PocketCoreUpdater updater, bool force = false)
    {
        if(!force) {
            ConsoleKey response;
            Console.Write("Do you want to overwrite existing json files? [y/N] ");
            Console.WriteLine("");
            response = Console.ReadKey(false).Key;
            if(response == ConsoleKey.Y) {
                force = true;
            }
        }
        await updater.BuildInstanceJSON(force);
    }

    static void RunCoreSelector(ref SettingsManager settings, List<Core> cores)
    {
        if(settings.GetConfig().download_new_cores?.ToLowerInvariant() == "yes") {
            foreach(Core core in cores)
                settings.EnableCore(core.identifier);
        } else {
            ConsoleKey response;
            Console.WriteLine("\nSelect your cores! The available cores will be listed 1 at a time. For each one, hit 'n' if you don't want it installed, or just hit enter if you want it. Ok you've got this. Here we go...\n");
            foreach(Core core in cores) {
                Console.Write(core.identifier + "?[Y/n] ");
                response = Console.ReadKey(false).Key;
                if(response == ConsoleKey.N) {
                    settings.DisableCore(core.identifier);
                } else {
                    settings.EnableCore(core.identifier);
                }
                Console.WriteLine("");
            }
        }
        settings.GetConfig().core_selector = false;
        settings.SaveSettings();
    }

    static void RunConfigWizard(ref SettingsManager settings)
    {
        ConsoleKey response;
        bool valid = false;
        while(!valid) {
            Console.Write("\nDownload Firmware Updates during 'Update All'?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if(response == ConsoleKey.N) {
                settings.GetConfig().download_firmware = false;
                valid = true;
            } else if(response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                settings.GetConfig().download_firmware = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        valid = false;
        while(!valid) {
            Console.Write("\nDownload Missing Assets (ROMs and BIOS Files) during 'Update All'?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if(response == ConsoleKey.N) {
                settings.GetConfig().download_assets = false;
                valid = true;
            } else if(response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                settings.GetConfig().download_assets = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        valid = false;
        while(!valid) {
            Console.Write("\nBuild game JSON files for supported cores during 'Update All'?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if(response == ConsoleKey.N) {
                settings.GetConfig().build_instance_jsons = false;
                valid = true;
            } else if(response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                settings.GetConfig().build_instance_jsons = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        valid = false;
        while(!valid) {
            Console.Write("\nDelete untracked cores during 'Update All'?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if(response == ConsoleKey.N) {
                settings.GetConfig().delete_skipped_cores = false;
                valid = true;
            } else if(response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                settings.GetConfig().delete_skipped_cores = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        valid = false;
        while(!valid) {
            Console.Write("\nAutomatically rename Jotego cores during 'Update All'?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if (response == ConsoleKey.N) {
                settings.GetConfig().fix_jt_names = false;
                valid = true;
            } else if (response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                settings.GetConfig().fix_jt_names = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        valid = false;
        while(!valid) {
            Console.Write("\nUse CRC check when checking ROMs and BIOS files?[Y/n] ");
            response = Console.ReadKey(false).Key;
            if (response == ConsoleKey.N) {
                settings.GetConfig().crc_check = false;
                valid = true;
            } else if (response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                settings.GetConfig().crc_check = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        valid = false;
        while(!valid) {
            Console.Write("\nPreserve 'Platforms' folder during 'Update All'?[y/N] ");
            response = Console.ReadKey(false).Key;
            if(response == ConsoleKey.N || response == ConsoleKey.Enter) {
                settings.GetConfig().preserve_platforms_folder = false;
                valid = true;
            } else if(response == ConsoleKey.Y) {
                settings.GetConfig().preserve_platforms_folder = true;
                valid = true;
            }
        }
        Console.WriteLine("");
        Console.WriteLine("Settings saved!");
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
        if(e.InstalledCores != null && e.InstalledCores.Count > 0) {
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
        if(e.SkippedAssets.Count > 0) {
            Console.WriteLine("Assets Not Found:");
            foreach(string asset in e.SkippedAssets) {
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
                    await HttpHelper.Instance.DownloadFileAsync(url, saveLocation);
                    Console.WriteLine("Download complete.");
                    Console.WriteLine(saveLocation);
                    Console.WriteLine("Go to " + releases[0].html_url + " for a change log");
                }
                return check;
            }

            return false;
        } catch(HttpRequestException e) {
            return false;
        }
    }

    // Check if a newer version is available
    async static Task AutoUpdateCheckVersion(string path)
    {
        try {
            string tempExecutable = Path.Combine(path, "update_temp.exe");

            // We can only delete after the old one is done running.
            if(File.Exists(tempExecutable)) {
                File.Delete(tempExecutable);
            }

            string? tagName, updateVersion, htmlUrl;

            // Get the available updates
            try {
                List<Github.Release> releases = await GithubApi.GetReleases(USER, REPOSITORY);

                tagName = releases[0].tag_name;
                updateVersion = SemverUtil.FindSemver(tagName);
                htmlUrl = releases[0].html_url;

                if(tagName == null || updateVersion == null || htmlUrl == null) {
                    throw new Exception();
                }
            } catch {
                Console.WriteLine("Unable to find updates on GitHub, continuing with original version.");
                return;
            }

            // Check if we actually need to update
            if(!SemverUtil.SemverCompare(updateVersion, version)) {
                Console.WriteLine("No new version found.");
                return;
            }

            Console.WriteLine("A new version is available. Downloading now...");

            string platform = GetPlatform();
            string url = String.Format(RELEASE_URL, tagName, platform);
            string saveLocation = Path.Combine(path, "pocket_updater.zip");

            // Download the update
            try {
                await HttpHelper.Instance.DownloadFileAsync(url, saveLocation);
            } catch {
                Console.WriteLine("Failed to download update, continuing with original version.");
                return;
            }

            Console.WriteLine("Download complete.");
            Console.WriteLine("Go to " + htmlUrl + " for a changelog");
            Console.WriteLine("Updating...");

            // Replace the current executable with the updated one
            string currentExecutable = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string updatedExecutable = Path.Combine(path, platform == "win" ? "pocket_updater.exe" : "pocket_updater");

            File.Move(currentExecutable, tempExecutable);

            // Replace the executable with the updated version from the ZIP file
            try {
                System.IO.Compression.ZipFile.ExtractToDirectory(saveLocation, path);

                if(!File.Exists(updatedExecutable)) {
                    throw new Exception();
                }
            } catch {
                // Undo the move and continue
                File.Move(tempExecutable, currentExecutable);
                Console.WriteLine("Something went wrong unzipping the update, continuing with original version.");
                return;
            }

            Console.WriteLine("Update complete, restarting...");

            // Start the updated executable
            var process = System.Diagnostics.Process.Start(updatedExecutable);

            // stdin only stays connected on Windows, wait on other platforms
            if(platform != "win") {
                // Can't do this on Windows because then deleting the old executable from the new one will fail
                process.WaitForExit();
            }

            // Terminate the old executable. Immediately on Windows, after the updated one finishes on other platforms.
            Environment.Exit(0);
        } catch {
            Console.WriteLine("Something went wrong with the update process, continuing with original version.");
            return;
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
        Random random = new Random();
        int i = random.Next(0, welcomeMessages.Length);
        string welcome = welcomeMessages[i];

        Console.WriteLine(welcome);

        foreach(var (item, index) in menuItems.WithIndex()) {
            Console.WriteLine($"{index}) {item}");
        }
        Console.Write("\nChoose your destiny: ");
        int choice;
        bool result = int.TryParse(Console.ReadLine(), out choice);
        if(result) {
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
            foreach(var (pack, index) in packs.WithIndex()) {
                Console.WriteLine($"{index}) {pack.owner}: {pack.repository} {pack.variant}");
            }
            Console.WriteLine($"{packs.Length}) Go back");
            Console.Write("\nSo, what'll it be?: ");
            int choice;
            bool result = int.TryParse(Console.ReadLine(), out choice);
            if(result && choice < packs.Length && choice >= 0) {
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

    private static void PauseExit(int exitcode = 0)
    {
        Console.ReadLine(); //wait for input so the console doesn't auto close in windows
        Environment.Exit(exitcode);
    }

    private static void Pause()
    {
        if(cliMode) return;
        Console.ReadLine();
    }

    private static void AskAboutNewCores(ref SettingsManager settings, bool force = false)
    {
        while(settings.GetConfig().download_new_cores == null || force) {
            force = false;

            Console.WriteLine("Would you like to, by default, install new cores? [Y]es, [N]o, [A]sk for each:");
            ConsoleKey response = Console.ReadKey(false).Key;
            settings.GetConfig().download_new_cores = response switch {
                ConsoleKey.Y => "yes",
                ConsoleKey.N => "no",
                ConsoleKey.A => "ask",
                _ => null
            };
        }
    }

    private static string[] menuItems = {
        "Update All",
        "Update Firmware",
        "Download Required Assets",
        "Select Cores",
        "Download Platform Image Packs",
        "Generate Instance JSON Files",
        "Settings",
        "Exit"
    };

    private static string[] welcomeMessages = {
        @"                                                                                
 _____ _                                          _ ___               _____       _ 
| __  | |___ _____ ___    _ _ ___ _ _ ___ ___ ___| |  _|   ___ ___   |   __|___ _| |
| __ -| | .'|     | -_|  | | | . | | |  _|_ -| -_| |  _|  | . |  _|  |  |  | . | . |
|_____|_|__,|_|_|_|___|  |_  |___|___|_| |___|___|_|_|    |___|_|    |_____|___|___|
                         |___|  ",
        @"                                                                                       
 _ _ _     _                      _          _____ _                 _                 
| | | |___| |___ ___ _____ ___   | |_ ___   |   __| |___ _ _ ___ ___| |_ ___ _ _ _ ___ 
| | | | -_| |  _| . |     | -_|  |  _| . |  |   __| | .'| | | . |  _|  _| . | | | |   |
|_____|___|_|___|___|_|_|_|___|  |_| |___|  |__|  |_|__,|\_/|___|_| |_| |___|_____|_|_|
                                                                                       ",
        @"                                                                                               
     _          ___ _       _                                       _            _             
 ___| |_ ___   |  _|_|___ _| |___    _ _ ___ _ _    ___ ___ _ _ ___| |_ _ _    _| |___ _ _ ___ 
|_ -|   | -_|  |  _| |   | . |_ -|  | | | . | | |  |  _|  _| | |_ -|  _| | |  | . | .'| | | -_|
|___|_|_|___|  |_| |_|_|_|___|___|  |_  |___|___|  |___|_| |___|___|_| |_  |  |___|__,|\_/|___|
                                    |___|                              |___|                   ",
        @"                                                                                                   
 _____ _   _        _               ___                                _                       _   
|_   _| |_|_|___   |_|___    ___   |  _|___ ___ ___ _ _    ___ ___ ___| |_ ___ _ _ ___ ___ ___| |_ 
  | | |   | |_ -|  | |_ -|  | .'|  |  _| .'|   |  _| | |  |  _| -_|_ -|  _| .'| | |  _| .'|   |  _|
  |_| |_|_|_|___|  |_|___|  |__,|  |_| |__,|_|_|___|_  |  |_| |___|___|_| |__,|___|_| |__,|_|_|_|  
                                                   |___|                                           ",
        @"                                                                                                             __ 
 _ _ _     _                      _          _   _          _____ _         _      _____         _       _  |  |
| | | |___| |___ ___ _____ ___   | |_ ___   | |_| |_ ___   | __  | |___ ___| |_   |     |___ ___| |_ ___| |_|  |
| | | | -_| |  _| . |     | -_|  |  _| . |  |  _|   | -_|  | __ -| | .'|  _| '_|  | | | | .'|  _| '_| -_|  _|__|
|_____|___|_|___|___|_|_|_|___|  |_| |___|  |_| |_|_|___|  |_____|_|__,|___|_,_|  |_|_|_|__,|_| |_,_|___|_| |__|
                                                                                                                ",
        @"                                                    
 _____ _       _            _____         _         
|     | |_ ___| |_ _ _     |_   _|___ ___| |_ _ _   
|-   -|  _|  _|   | | |_     | | | .'|_ -|  _| | |_ 
|_____|_| |___|_|_|_  |_|    |_| |__,|___|_| |_  |_|
                  |___|                      |___|  ",
        @"                   _                                       _____ 
 _ _ _ _       _  | |                      _           _  |___  |
| | | | |_ ___| |_|_|___ ___    _ _ ___   | |_ _ _ _ _|_|___|  _|
| | | |   | .'|  _| |  _| -_|  | | | .'|  | . | | | | | |   |_|  
|_____|_|_|__,|_|   |_| |___|  |_  |__,|  |___|___|_  |_|_|_|_|  
                               |___|              |___|          ",
        @"                                                  _____ 
 _ _ _ _       _      _                          |___  |
| | | | |_ ___| |_   |_|___    ___    _____ ___ ___|  _|
| | | |   | .'|  _|  | |_ -|  | .'|  |     | .'|   |_|  
|_____|_|_|__,|_|    |_|___|  |__,|  |_|_|_|__,|_|_|_|  
                                                        ",
        @"                                                    
 _____ _         _            _____     _ _       _ 
|   __|_|___ ___|_|___ ___   |     |___|_| |___ _| |
|   __| |_ -|_ -| | . |   |  | | | | .'| | | -_| . |
|__|  |_|___|___|_|___|_|_|  |_|_|_|__,|_|_|___|___|
                                                    ",
        @"                             
 _____       _               
|  |  |_____| |_ ___ ___ ___ 
|  |  |     | . | .'|_ -| .'|
|_____|_|_|_|___|__,|___|__,|
                             ",
        
    };
}

public class Options
{
    [Option('u', "update", HelpText = "Force updater to just run update process, instead of displaying the menu.", Required = false)]
    public bool ForceUpdate { get; set; }
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

    [Option ('c', "coreselector", Required = false, HelpText = "Run the core selector.")]
    public bool CoreSelector { get; set; }

    [Option('f', "platformsfolder", Required = false, HelpText = "Preserve the Platforms folder, so customizations aren't overwritten by updates.")]
    public bool PreservePlatformsFolder { get; set; }

    [Option('i', "instancegenerator", HelpText = "Force updater to just run instance json generator, instead of displaying the menu.", Required = false)]
    public bool ForceInstanceGenerator { get; set; }
}

public static class EnumExtension
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
       => self.Select((item, index) => (item, index));
}
