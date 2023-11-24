using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using CommandLine;
using pannella.analoguepocket;
using ConsoleTools;


internal class Program
{
    private static string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    private const string USER = "mattpannella";
    private const string REPOSITORY = "pocket-updater-utility";
    private const string RELEASE_URL = "https://github.com/mattpannella/pocket-updater-utility/releases/download/{0}/pocket_updater_{1}.zip";
    private static SettingsManager settings;

    private static PocketCoreUpdater updater;

    private static bool cliMode = false;
    private static async Task Main(string[] args)
    {
        try {
            string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string? path = Path.GetDirectoryName(location);
            bool preservePlatformsFolder = false;
            bool forceUpdate = false;
            bool forceInstanceGenerator = false;
            string? downloadAssets = null;
            string? coreName = null;
            string? imagePackOwner = null;
            string? imagePackRepo = null;
            string? imagePackVariant = null;
            bool downloadFirmware = false;
            bool selfUpdate = false;

            ConsoleKey response;

            string verb = "menu";
            Dictionary<string, object?> data = new Dictionary<string, object?>();
            Parser.Default.ParseArguments<MenuOptions, FundOptions, UpdateOptions,
                AssetsOptions, FirmwareOptions, ImagesOptions, InstancegeneratorOptions, UpdateSelfOptions>(args)
                .WithParsed<UpdateSelfOptions>(o => {
                    selfUpdate = true;
                })
                .WithParsed<FundOptions>(async o =>
                {
                    verb = "fund";
                    data.Add("core", null);
                    if(o.Core != null && o.Core != "") {
                        data["core"] = o.Core;
                    }
                }
                )
                .WithParsed<UpdateOptions>(async o =>
                {
                    verb = "update";
                    cliMode = true;
                    forceUpdate = true;
                    if(o.InstallPath != null && o.InstallPath != "") {
                        path = o.InstallPath;
                    }
                    if(o.PreservePlatformsFolder) {
                        preservePlatformsFolder = true;
                    }
                    if(o.CoreName != null && o.CoreName != "") {
                        coreName = o.CoreName;
                    }
                }
                )
                .WithParsed<AssetsOptions>(async o =>
                {
                    verb = "assets";
                    cliMode = true;
                    downloadAssets = "all";
                    if(o.InstallPath != null && o.InstallPath != "") {
                        path = o.InstallPath;
                    }
                    if(o.CoreName != null) {
                        downloadAssets = o.CoreName;
                    }
                }
                )
                .WithParsed<FirmwareOptions>(async o =>
                {
                    verb = "firmware";
                    cliMode = true;
                    downloadFirmware = true;
                    if(o.InstallPath != null && o.InstallPath != "") {
                        path = o.InstallPath;
                    }
                }
                )
                .WithParsed<ImagesOptions>(async o =>
                {
                    verb = "images";
                    cliMode = true;
                    if(o.InstallPath != null && o.InstallPath != "") {
                        path = o.InstallPath;
                    }
                    if(o.ImagePackOwner != null) {
                        imagePackOwner = o.ImagePackOwner;
                        imagePackRepo = o.ImagePackRepo;
                        imagePackVariant = o.ImagePackVariant;
                    }
                }
                )
                .WithParsed<InstancegeneratorOptions>(async o =>
                {
                    verb = "instancegenerator";
                    forceInstanceGenerator = true;
                    cliMode = true;
                    if(o.InstallPath != null && o.InstallPath != "") {
                        path = o.InstallPath;
                    }
                }
                )
                .WithParsed<MenuOptions>(o =>
                {
                    if(o.InstallPath != null && o.InstallPath != "") {
                        path = o.InstallPath;
                    }
                }
                )
                .WithNotParsed(o =>
                {
                    if(o.IsHelp()) {
                        Environment.Exit(1);
                    }
                    if(o.IsVersion()) {
                        Environment.Exit(1);
                    }
                }
                );

            if (!cliMode) {
                Console.WriteLine("Pocket Updater Utility v" + version);
                Console.WriteLine("Checking for updates...");

                if(await CheckVersion(path) && !selfUpdate) {
                    string platform = GetPlatform();
                    ConsoleKey[] acceptedInputs = new[] { ConsoleKey.I, ConsoleKey.C, ConsoleKey.Q };
                    do {
                        if (platform == "win") {
                            Console.Write("Would you like to [i]nstall the update, [c]ontinue with the current version, or [q]uit? [i/c/q]: ");
                        } else {
                            Console.Write("Update downloaded. Would you like to [c]ontinue with the current version, or [q]uit? [c/q]: ");
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
                if(selfUpdate) {
                    Environment.Exit(0);
                }
            }

            //path = "/Users/mattpannella/pocket-test";

            updater = new PocketCoreUpdater(path);
            settings = new SettingsManager(path);

            switch(verb) {
                case "fund":
                    await Funding((string)data["core"]);
                    Environment.Exit(1);
                    break;
                default:
                    break;
            }

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
            settings = GlobalHelper.Instance.SettingsManager;

            // If we have any missing cores, handle them.
            if(updater.GetMissingCores().Any()) {
                Console.WriteLine("\nNew cores found since the last run.");
                AskAboutNewCores();

                string? download_new_cores = settings.GetConfig().download_new_cores?.ToLowerInvariant();
                switch(download_new_cores) {
                    case "yes":
                        Console.WriteLine("The following cores have been enabled:");
                        foreach(Core core in updater.GetMissingCores())
                            Console.WriteLine($"- {core.identifier}");

                        settings.EnableMissingCores(updater.GetMissingCores());
                        settings.SaveSettings();
                        break;
                    case "no":
                        Console.WriteLine("The following cores have been disabled:");
                        foreach(Core core in updater.GetMissingCores())
                            Console.WriteLine($"- {core.identifier}");

                        settings.DisableMissingCores(updater.GetMissingCores());
                        settings.SaveSettings();
                        break;
                    default:
                    case "ask":
                        var newones = updater.GetMissingCores();
                        settings.EnableMissingCores(newones);
                        if (cliMode) {
                            settings.SaveSettings();
                        } else {
                            await RunCoreSelector(newones, "New cores are available!");
                        }
                        break;
                }

                updater.LoadSettings();
            }
            if(forceUpdate) {
                Console.WriteLine("Starting update process...");
                await updater.RunUpdates(coreName);
                Pause();
            } else if(downloadFirmware) {
                await updater.UpdateFirmware();
            } else if(forceInstanceGenerator) {
                await RunInstanceGenerator(updater, true);
            } else if(downloadAssets != null) {
                if (downloadAssets == "all") {
                    await updater.RunAssetDownloader();
                } else {
                    await updater.RunAssetDownloader(downloadAssets);
                }
            } else if (imagePackOwner != null) {
                ImagePack pack = new ImagePack() {
                    owner = imagePackOwner,
                    repository = imagePackRepo,
                    variant = imagePackVariant
                };
                await InstallImagePack(path, pack);
            } else { 
                bool flag = true;
                while(flag) {
                    int choice = DisplayMenuNew();

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
                            AskAboutNewCores(true);
                            await RunCoreSelector(cores);
                            updater.LoadSettings();
                            break;
                        case 4:
                            await ImagePackSelector(path);
                            break;
                        case 5:
                            await RunInstanceGenerator(updater);
                            Pause();
                            break;
                        case 6:
                            await BuildGameandWatchROMS(path);
                            Pause();
                            break;
                        case 7:
                            SettingsMenu();
                            SetUpdaterFlags();
                            break;
                        case 8:
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

    private async static Task BuildGameandWatchROMS(string directory)
    {
        Github.Release release = await GithubApi.GetLatestRelease("agg23", "fpga-gameandwatch");
        foreach(Github.Asset asset in release.assets) {
            if (asset.name.EndsWith("Tools.zip")) {
                string downloadPath = Path.Combine(directory, "tools", "gameandwatch");
                string filename = Path.Combine(downloadPath, asset.name);
                if(!File.Exists(filename)) {
                    Directory.CreateDirectory(downloadPath);
                    await HttpHelper.Instance.DownloadFileAsync(asset.browser_download_url, filename);
                    ZipFile.ExtractToDirectory(filename, downloadPath, true);
                }
                break;
            }
        }
        string execName = "fpga-gnw-romgenerator";
        string execLocation = Path.Combine(directory, "tools", "gameandwatch");
        string manifestPath = Path.Combine(directory, "tools", "gameandwatch");
        switch(GetPlatform()) {
            case "win":
                execName += ".exe";
                execLocation = Path.Combine(execLocation, "windows", execName);
                manifestPath = Path.Combine(manifestPath, "windows", "manifest.json");
                break;
            case "mac":
                execLocation = Path.Combine(execLocation, "mac", execName);
                manifestPath = Path.Combine(manifestPath, "mac", "manifest.json");
                Exec($"chmod +x {execLocation}");
                break;
            default:
                execLocation = Path.Combine(execLocation, "linux", execName);
                manifestPath = Path.Combine(manifestPath, "linux", "manifest.json");
                Exec($"chmod +x {execLocation}");
                break;
        }

        string romLocation = Path.Combine(directory, "Assets", "gameandwatch", "agg23.GameAndWatch");
        string outputLocation = Path.Combine(directory, "Assets", "gameandwatch", "common");

        try {
            // Execute
            Console.WriteLine($"Executing {execLocation}");
            ProcessStartInfo pInfo = new ProcessStartInfo(execLocation) {
                Arguments = $"--mame-path \"{romLocation}\" --output-path \"{outputLocation}\" --manifest-path \"{manifestPath}\" supported",
                UseShellExecute = false
            };

            Process p = Process.Start(pInfo);
            p.WaitForExit();
        } catch(Exception e) {
            Console.Error.WriteLine($"An error occurred: {e.GetType().Name}:{e.ToString()}");
        }
    }

    static void SetUpdaterFlags()
    {
        updater.DeleteSkippedCores(settings.GetConfig().delete_skipped_cores);
        updater.SetGithubApiKey(settings.GetConfig().github_token);
        updater.DownloadFirmware(settings.GetConfig().download_firmware);
        updater.DownloadAssets(settings.GetConfig().download_assets);
        updater.RenameJotegoCores(settings.GetConfig().fix_jt_names);
        updater.LoadSettings();
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

    public static void Exec(string cmd)
    {
        var escapedArgs = cmd.Replace("\"", "\\\"");
            
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\""
            }
        };

        process.Start();
        process.WaitForExit();
    }

    static async Task RunCoreSelector(List<Core> cores, string message = "Select your cores.")
    {
        if(settings.GetConfig().download_new_cores?.ToLowerInvariant() == "yes") {
            foreach(Core core in cores)
                settings.EnableCore(core.identifier);
        } else {
            var pageSize = 15;
            var offset = 0;
            bool next = false;
            bool previous = false;
            bool more = true;
            while(more) {
                var menu = new ConsoleMenu()
                    .Configure(config =>
                    {
                        config.Selector = "=>";
                        config.EnableWriteTitle = false;
                        config.WriteHeaderAction = () => Console.WriteLine($"{message} Use enter to check/uncheck your choices.");
                        config.SelectedItemBackgroundColor = Console.ForegroundColor;
                        config.SelectedItemForegroundColor = Console.BackgroundColor;
                        config.WriteItemAction = item => Console.Write("{1}", item.Index, item.Name);
                    });
                var current = -1;
                if((offset + pageSize) <= cores.Count) {
                    menu.Add("Next Page", (thisMenu) => { offset += pageSize; thisMenu.CloseMenu();});
                }
                foreach(Core core in cores) {
                    current++;
                    if ((current <= (offset + pageSize)) && (current >= offset)) {
                        var coreSettings = settings.GetCoreSettings(core.identifier);
                        var selected = !coreSettings.skip;
                        var name = core.identifier;
                        if (core.requires_license) {
                            name += " (Requires beta access)";
                        }
                        var title = settingsMenuItem(name, selected);
                        menu.Add(title, (thisMenu) => { 
                            selected = !selected;
                            if (!selected) {
                                settings.DisableCore(core.identifier);
                            } else {
                                settings.EnableCore(core.identifier);
                            }

                            thisMenu.CurrentItem.Name = settingsMenuItem(core.identifier, selected);
                        });
                    }
                }
                if((offset + pageSize) <= cores.Count) {
                    menu.Add("Next Page", (thisMenu) => { offset += pageSize; thisMenu.CloseMenu();});
                }
                menu.Add("Save Choices", (thisMenu) => {thisMenu.CloseMenu(); more = false;});
                menu.Show();
            }
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
        if(e.MissingBetaKeys.Count > 0) {
            Console.WriteLine("Missing or incorrect Beta Key for the following cores:");
            foreach(string core in e.MissingBetaKeys) {
                Console.WriteLine(core);
            }
            Console.WriteLine("");
        }
        ShowSponsorLinks();
        FunFacts();
    }

    private static void ShowSponsorLinks()
    {
        if (GlobalHelper.Instance.InstalledCores.Count == 0) return;
        var random = new Random();
        var index = random.Next(GlobalHelper.Instance.InstalledCores.Count);
        var randomItem = GlobalHelper.Instance.InstalledCores[index];
        if(randomItem.sponsor != null) {
            var links = "";
            if (randomItem.sponsor.custom != null) {
                links += "\r\n" + String.Join("\r\n", randomItem.sponsor.custom);
            }
            if (randomItem.sponsor.github != null) {
                links += "\r\n" + String.Join("\r\n", randomItem.sponsor.github);
            }
            if (randomItem.sponsor.patreon != null) {
                links += "\r\n" + randomItem.sponsor.patreon;
            }
            Console.WriteLine("");
            Console.WriteLine($"Please consider supporting {randomItem.getConfig().core.metadata.author} for their work on the {randomItem} core:");
            Console.WriteLine(links.Trim());
        }
    }

    private static string? GetSponsorLinks()
    {
        if (GlobalHelper.Instance.InstalledCores.Count == 0) return null;
        var random = new Random();
        var index = random.Next(GlobalHelper.Instance.InstalledCores.Count);
        var randomItem = GlobalHelper.Instance.InstalledCores[index];
        string output = "";
        if(randomItem.sponsor != null) {
            var links = "";
            if (randomItem.sponsor.custom != null) {
                links += "\r\n" + String.Join("\r\n", randomItem.sponsor.custom);
            }
            if (randomItem.sponsor.github != null) {
                links += "\r\n" + String.Join("\r\n", randomItem.sponsor.github);
            }
            if (randomItem.sponsor.patreon != null) {
                links += "\r\n" + randomItem.sponsor.patreon;
            }
            output += "\r\n";
            output += $"Please consider supporting {randomItem.getConfig().core.metadata.author} for their work on the {randomItem} core:";
            output += $"\r\n{links.Trim()}";
        }

        return output;
    }

    private static async Task Funding(string? identifier)
    {
        await updater.Initialize();
        if (GlobalHelper.Instance.InstalledCores.Count == 0) return;

        List<Core> cores = new List<Core>();
        if (identifier == null) {
            cores = GlobalHelper.Instance.InstalledCores;
        } else {
            var c = GlobalHelper.Instance.GetCore(identifier);
            if (c != null && c.isInstalled()) {
                cores.Add(c);
            }
        }
        
        foreach(Core core in cores) {
            if(core.sponsor != null) {
                var links = "";
                if (core.sponsor.custom != null) {
                    links += "\r\n" + String.Join("\r\n", core.sponsor.custom);
                }
                if (core.sponsor.github != null) {
                    links += "\r\n" + String.Join("\r\n", core.sponsor.github);
                }
                if (core.sponsor.patreon != null) {
                    links += "\r\n" + core.sponsor.patreon;
                }
                Console.WriteLine("");
                Console.WriteLine($"{core.identifier}:");
                Console.WriteLine(links.Trim());
            }
        }
    }

    private static void FunFacts()
    {
        if (GlobalHelper.Instance.InstalledCores.Count == 0) return;
        List<string> cores = new List<string>();

        foreach(Core c in GlobalHelper.Instance.InstalledCores) {
            if (c.getConfig().core.framework.sleep_supported) {
                cores.Add(c.identifier);
            }
        }
        Console.WriteLine("");
        string list = String.Join(", ", cores.ToArray());
        Console.WriteLine("Fun fact! The ONLY cores that support save states and sleep are the following:");
        Console.WriteLine(list);
        Console.WriteLine("Please don't bother the developers of the other cores about this feature. It's a lot of work and most likely will not be coming.");
        
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
                    await Factory.GetHttpHelper().DownloadFileAsync(url, saveLocation);
                    Console.WriteLine("Download complete.");
                    Console.WriteLine(saveLocation);
                    Console.WriteLine("Go to " + releases[0].html_url + " for a change log");
                } else {
                    Console.WriteLine("Up to date.");
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
                await Factory.GetHttpHelper().DownloadFileAsync(url, saveLocation);
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
            Architecture arch = RuntimeInformation.ProcessArchitecture;
            if(arch == Architecture.Arm64) {
                return "linux_arm64";
            } else if(arch == Architecture.Arm) {
                return "linux_arm32";
            }
            return "linux";
        }

        return "";
    }

    private static int DisplayMenuNew()
    {
        Console.Clear();
        Random random = new Random();
        int i = random.Next(0, welcomeMessages.Length);
        string welcome = welcomeMessages[i];

        int choice = 0;

        var menu = new ConsoleMenu()
            .Configure(config =>
            {
            config.Selector = "=>";
            //config.EnableFilter = true;
            config.Title = $"{welcome}\r\n{GetSponsorLinks()}\r\n";
            config.EnableWriteTitle = true;
            //config.EnableBreadcrumb = true;
            config.WriteHeaderAction = () => Console.WriteLine("Choose your destiny:");
            config.SelectedItemBackgroundColor = Console.ForegroundColor;
            config.SelectedItemForegroundColor = Console.BackgroundColor;
            });
        
        foreach(var (item, index) in menuItems.WithIndex()) {
            menu.Add(item, (thisMenu) => { choice = thisMenu.CurrentItem.Index; thisMenu.CloseMenu(); });
        }

        menu.Show();
      
        return choice;
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
        ShowSponsorLinks();
        Console.Write("\nChoose your destiny: ");
        int choice;
        bool result = int.TryParse(Console.ReadLine(), out choice);
        if(result) {
            return choice;
        }
        return 0;
    }

    private static async Task SettingsMenu()
    {
        Console.Clear();

        var menuItems = new Dictionary<string, string>(){
            {"download_firmware", "Download Firmware Updates during 'Update All'"},
            {"download_assets", "Download Missing Assets (ROMs and BIOS Files) during 'Update All'"},
            {"build_instance_jsons", "Build game JSON files for supported cores during 'Update All'"},
            {"delete_skipped_cores", "Delete untracked cores during 'Update All'"},
            {"fix_jt_names", "Automatically rename Jotego cores during 'Update All"},
            {"crc_check", "Use CRC check when checking ROMs and BIOS files"},
            {"preserve_platforms_folder", "Preserve 'Platforms' folder during 'Update All'"},
            {"skip_alternative_assets", "Skip alternative roms when downloading assets"},
            {"use_custom_archive", "Use custom asset archive"}
        };

        var type = typeof(Config);

        var menu = new ConsoleMenu()
            .Configure(config =>
            {
                config.Selector = "=>";
                config.EnableWriteTitle = false;
                config.WriteHeaderAction = () => Console.WriteLine("Settings. Use enter to check/uncheck your choices.");
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
                config.WriteItemAction = item => Console.Write("{1}", item.Index, item.Name);
            });
        
        foreach(var (name, text) in menuItems) {
            var property = type.GetProperty(name);
            var value = (bool)property.GetValue(settings.GetConfig());
            var title = settingsMenuItem(text, value);
            menu.Add(title, (thisMenu) => { value = !value; property.SetValue(settings.GetConfig(), value); thisMenu.CurrentItem.Name =  settingsMenuItem(text, value);});
        }
        
        menu.Add("Save", (thisMenu) => {thisMenu.CloseMenu();});

        menu.Show();

        settings.SaveSettings();
    }

    private static string settingsMenuItem(string title, bool value)
    {
        var x = " ";
        if (value) {
            x = "X";
        }

        return $"[{x}] {title}";
    }

    private static async Task ImagePackSelector(string path)
    {
        Console.Clear();
        Console.WriteLine("Checking for image packs...\n");
        ImagePack[] packs = await ImagePacksService.GetImagePacks();
        if(packs.Length > 0) {
            int choice = 0;
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                config.Selector = "=>";
                config.EnableWriteTitle = false;
                //config.EnableBreadcrumb = true;
                config.WriteHeaderAction = () => Console.WriteLine("So, what'll it be?:");
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
                });
            
            foreach(var (pack, index) in packs.WithIndex()) {
                menu.Add($"{pack.owner}: {pack.repository} {pack.variant}", (thisMenu) => { choice = thisMenu.CurrentItem.Index; thisMenu.CloseMenu(); });
            }
            menu.Add("Go Back", (thisMenu) => {choice = packs.Length; thisMenu.CloseMenu();});

            menu.Show();

            if(choice < packs.Length && choice >= 0) {
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
        await pack.Install(path);
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

    private static void AskAboutNewCores(bool force = false)
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
        "Generate Game and Watch ROMS",
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
        @"               _                              
 __        _  | |                             
|  |   ___| |_|_|___    _____ ___ ___ ___ _ _ 
|  |__| -_|  _| |_ -|  |     | . |_ -| -_| | |
|_____|___|_|   |___|  |_|_|_|___|___|___|_  |
                                         |___|",

        
    };
}
[Verb("menu", isDefault: true, HelpText = "Interactive Main Menu")]
public class MenuOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }
}

[Verb("update",  HelpText = "Run update all. (You can configure via the settings menu)")]
public class UpdateOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

    [Option ('c', "core", Required = false, HelpText = "The core you want to update.")]
    public string CoreName { get; set; }

    [Option('f', "platformsfolder", Required = false, HelpText = "Preserve the Platforms folder, so customizations aren't overwritten by updates.")]
    public bool PreservePlatformsFolder { get; set; }
}

[Verb("assets",  HelpText = "Run the asset downloader")]
public class AssetsOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

    [Option ('c', "core", Required = false, HelpText = "The core you want to download assets for.")]
    public string CoreName { get; set; }
}

[Verb("instancegenerator",  HelpText = "Run the instance JSON generator")]
public class InstancegeneratorOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }
}

[Verb("images",  HelpText = "Download image packs")]
public class ImagesOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

    [Option('o', "owner", Required = false, HelpText = "Image pack repo username")]
    public string ImagePackOwner { get; set; }

    [Option('i', "imagepack", Required = false, HelpText = "Github repo name for image pack")]
    public string ImagePackRepo { get; set; }

    [Option('v', "variant", Required = false, HelpText = "The optional variant")]
    public string? ImagePackVariant { get; set; }
}

[Verb("firmware",  HelpText = "Check for Pocket firmware updates")]
public class FirmwareOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }
}

[Verb("fund", HelpText = "List sponsor links")]
public class FundOptions
{
    [Option('c', "core", HelpText = "The core to check funding links for", Required = false)]
    public string? Core { get; set; }
}

[Verb("update-self", HelpText = "Update this utility")]
public class UpdateSelfOptions
{
}

public static class EnumExtension
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
       => self.Select((item, index) => (item, index));
}
