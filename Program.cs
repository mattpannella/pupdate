internal class Program
{
    private static async Task Main(string[] args)
    {
        //string path = "/Users/matt/pocket-test/";

        string location = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        string path = Path.GetDirectoryName(location);
        string cores = path + "/auto_update.json";
        
        //string cores = "/Users/matt/development/c#/pocket_updater/auto_update.json";
        Updater updater = new Updater(cores, path);
        
        Console.WriteLine("Starting update process...");
        await updater.runUpdates();
        
        Console.WriteLine("and now its done");
    }
}