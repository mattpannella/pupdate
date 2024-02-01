namespace Pannella;

internal partial class Program
{
    private static async Task RunInstanceGenerator(PocketCoreUpdater pocketCoreUpdater, bool force = false)
    {
        if (!force)
        {
            Console.Write("Do you want to overwrite existing json files? [Y/N] ");
            Console.WriteLine("");

            var response = Console.ReadKey(false).Key;

            if (response == ConsoleKey.Y)
            {
                force = true;
            }
        }

        await pocketCoreUpdater.BuildInstanceJson(force);
    }
}
