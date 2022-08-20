using pannella.analoguepocket;
using System.Net.Http.Headers;
using System.Text.Json;

internal class Program
{
    private const string VERSION = "1.1.1";
    private const string API_URL = "https://api.github.com/repos/mattpannella/pocket_core_autoupdate_net/releases";
    private static async Task Main(string[] args)
    {
        if(await CheckVersion()) {
            Console.WriteLine("A new version of this utility is availailable. Go to this url to download it:");
            Console.WriteLine("https://github.com/mattpannella/pocket_core_autoupdate_net/releases");
            Console.WriteLine("Would you like to continue anyway? [y/n]:");
            ConsoleKey response = Console.ReadKey(false).Key;
            if (response == ConsoleKey.N) {
                Console.WriteLine("Come again soon");
                Console.ReadLine(); //wait for input so the console doesn't auto close in windows
                Environment.Exit(1);
            }
        }

        string path = "/Users/matt/pocket-test";
        string cores = "/Users/matt/development/c#/pocket_updater/auto_update.json";

        //string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        //string path = Path.GetDirectoryName(location);
        //string cores = path + "/auto_update.json";
        
        PocketCoreUpdater updater = new PocketCoreUpdater(cores, path);

        updater.StatusUpdated += updater_StatusUpdated;
        updater.InstallBiosFiles(true);

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
                return SemverUtil.SemverCompare(v, VERSION);
            }

            return false;
        } catch (HttpRequestException e) {
            return false;
        }
    }
}