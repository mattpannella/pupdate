using pannella.analoguepocket;
using System.Net.Http.Headers;
using System.Text.Json;
using CommandLine;

internal class Program
{
    private static string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    private const string API_URL = "https://api.github.com/repos/mattpannella/pocket_core_autoupdate_net/releases";

    private const string REMOTE_CORES_FILE = "https://raw.githubusercontent.com/mattpannella/pocket_core_autoupdate_net/main/pocket_updater_cores.json";
    private static async Task Main(string[] args)
    {
        bool autoUpdate = false;
        string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        string path = Path.GetDirectoryName(location);
        bool extractAll = false;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(o =>
            {
                if(o.Update) {
                    autoUpdate = true;
                }
                if(o.InstallPath != null && o.InstallPath != "") {
                    Console.WriteLine("path: " + o.InstallPath);
                    path = o.InstallPath;
                }
                if(o.ExtractAll) {
                    extractAll = true;
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

        ConsoleKey response;

        Console.WriteLine("Analogue Pocket Core Updater v" + version);
        Console.WriteLine("Checking for updates...");
        if(await CheckVersion()) {
            Console.WriteLine("A new version is available. Go to this url to download it:");
            Console.WriteLine("https://github.com/mattpannella/pocket_core_autoupdate_net/releases");
            Console.WriteLine("Would you like to continue anyway? [y/n]:");
            response = Console.ReadKey(false).Key;
            if (response == ConsoleKey.N) {
                Console.WriteLine("Come again soon");
                Console.ReadLine(); //wait for input so the console doesn't auto close in windows
                Environment.Exit(1);
            }
        }

        //path = "/Users/mattpannella/pocket-test";
        //string cores = "/Users/mattpannella/development/c#/pocket_updater/pocket_updater_cores.json";

        
        string cores = path + "/pocket_updater_cores.json";
        if(!File.Exists(cores)) {
            autoUpdate = true;
        }

        if(!autoUpdate) {
            Console.WriteLine("Download master cores list file from github? (This will overwrite your current file) [y/n]: (default is y)");
            response = Console.ReadKey(false).Key;
            if (response == ConsoleKey.Y || response == ConsoleKey.Enter) {
                await DownloadCoresFile(cores);
            }
        } else {
            await DownloadCoresFile(cores);
        }
        
        PocketCoreUpdater updater = new PocketCoreUpdater(path, cores);
        SettingsManager settings = new SettingsManager(path);

        updater.ExtractAll(extractAll);
        
        updater.SetGithubApiKey(settings.GetConfig().github_token);
        updater.DownloadFirmware(settings.GetConfig().download_firmware);
        updater.StatusUpdated += updater_StatusUpdated;
        updater.DownloadAssets(settings.GetConfig().download_assets);
        Console.WriteLine("Starting update process...");

        await updater.RunUpdates();
        
        Console.WriteLine("and now its done");
        Console.ReadLine(); //wait for input so the console doesn't auto close in windows
    }

    async static Task DownloadCoresFile(string file)
    {
        try {
            Console.WriteLine("Downloading cores file...");
            await HttpHelper.DownloadFileAsync(REMOTE_CORES_FILE, file);
            Console.WriteLine("Download complete:");
            Console.WriteLine(file);
        } catch (UnauthorizedAccessException e) {
            Console.WriteLine("Unable to save file.");
            Console.WriteLine(e.Message);
            Console.ReadLine();
            Environment.Exit(1);
        }
    }

    static void updater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    //return true if newer version is available
    async static Task<bool> CheckVersion()
    {
        try {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(API_URL)
            };
            var agent = new ProductInfoHeaderValue("Analogue-Pocket-Auto-Updater", "1.0");
            request.Headers.UserAgent.Add(agent);
            var response = await client.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            List<Github.Release>? releases = JsonSerializer.Deserialize<List<Github.Release>>(responseBody);

            string tag_name = releases[0].tag_name;
            string? v = SemverUtil.FindSemver(tag_name);
            if(v != null) {
                return SemverUtil.SemverCompare(v, version);
            }

            return false;
        } catch (HttpRequestException e) {
            return false;
        }
    }
}

public class Options
{
    [Option ('u', "update", Required = false, HelpText = "Automatically download newest core list without asking.")]
    public bool Update { get; set; }

    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

     [Option ('a', "all", Required = false, HelpText = "Extract all release assets, instead of just ones containing openFPGA cores.")]
     public bool ExtractAll { get; set; }
}