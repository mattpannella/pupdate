using pannella.analoguepocket;

internal class Program
{
    private static async Task Main(string[] args)
    {
        //string path = "/Users/matt/pocket-test";
        //string cores = "/Users/matt/development/c#/pocket_updater/auto_update.json";

        string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        string path = Path.GetDirectoryName(location);
        string cores = path + "/auto_update.json";
        
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
}