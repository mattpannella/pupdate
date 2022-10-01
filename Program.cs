using pannella.analoguepocket;
using System.Net.Http.Headers;
using System.Text.Json;
using CommandLine;

internal class Program
{
    private static string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    private const string API_URL = "https://api.github.com/repos/mattpannella/pocket_core_autoupdate_net/releases";
    private static async Task Main(string[] args)
    {
        bool autoUpdate = false;
        string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        string path = Path.GetDirectoryName(location);
        bool extractAll = false;

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

        PocketCoreUpdater updater = new PocketCoreUpdater(path);
        SettingsManager settings = new SettingsManager(path);

        updater.ExtractAll(extractAll);
        
        updater.SetGithubApiKey(settings.GetConfig().github_token);
        updater.DownloadFirmware(settings.GetConfig().download_firmware);
        updater.StatusUpdated += updater_StatusUpdated;
        updater.DownloadAssets(settings.GetConfig().download_assets);
        await updater.Initialize();

        Console.WriteLine("Starting update process...");

        await updater.RunUpdates();
        
        Console.WriteLine("and now its done");
        Console.ReadLine(); //wait for input so the console doesn't auto close in windows
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
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string? InstallPath { get; set; }

     [Option ('a', "all", Required = false, HelpText = "Extract all release assets, instead of just ones containing openFPGA cores.")]
     public bool ExtractAll { get; set; }
}