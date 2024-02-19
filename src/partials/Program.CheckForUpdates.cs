namespace Pannella;

internal partial class Program
{
    private static async Task CheckForUpdates(string path, bool selfUpdate, string[] args)
    {
        Console.WriteLine("Pupdate v" + VERSION);
        Console.WriteLine("Checking for updates...");

        if (await CheckVersion(path) && !selfUpdate)
        {
            ConsoleKey[] acceptedInputs = { ConsoleKey.I, ConsoleKey.C, ConsoleKey.Q };
            ConsoleKey response;

            do
            {
                Console.Write(SYSTEM_OS_PLATFORM is "win" or "linux" or "mac"
                    ? "Would you like to [i]nstall the update, [c]ontinue with the current version, or [q]uit? [i/c/q]: "
                    : "Update downloaded. Would you like to [c]ontinue with the current version, or [q]uit? [c/q]: ");

                response = Console.ReadKey(false).Key;
                Console.WriteLine();
            }
            while (!acceptedInputs.Contains(response));

            switch (response)
            {
                case ConsoleKey.I:
                    int result = UpdateSelfAndRun(path, args);
                    Environment.Exit(result);
                    break;

                case ConsoleKey.C:
                    break;

                case ConsoleKey.Q:
                    Console.WriteLine("Come again soon!");
                    Pause();
                    break;
            }
        }

        if (selfUpdate)
        {
            Environment.Exit(0);
        }
    }
}
