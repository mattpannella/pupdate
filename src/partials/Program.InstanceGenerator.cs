using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static void RunInstanceGenerator(CoreUpdaterService coreUpdaterService, bool force = false)
    {
        if (!force)
        {
            Console.Write("Do you want to overwrite existing json files? [Y/N] ");
            Console.WriteLine();

            var response = Console.ReadKey(false).Key;

            if (response == ConsoleKey.Y)
            {
                force = true;
            }
        }

        coreUpdaterService.BuildInstanceJson(force);
    }
}
