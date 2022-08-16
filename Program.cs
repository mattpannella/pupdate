internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        Updater updater = new Updater();
        string v1 = "1.1";
        string v2 = "1.5.0";

        Console.WriteLine(updater.semverFix(v1));
        Console.WriteLine(updater.semverFix(v2));

        Console.WriteLine("i'm going to download a zip");
        //await updater.updateCore("https://github.com/mattpannella/pocket_core_autoupdate/archive/refs/tags/v1.0.1.zip");
        await updater.runUpdates();
        Console.WriteLine("and now its done");
    }
}