using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace Pannella;

internal partial class Program
{
    private static int UpdateSelfAndRun(string directory, string[] updaterArgs)
    {
        string execName = "pupdate";

        if (SYSTEM_OS_PLATFORM == "win")
        {
            execName += ".exe";
        }

        string execLocation = Path.Combine(directory, execName);
        string backupName = $"{execName}.backup";
        string backupLocation = Path.Combine(directory, backupName);
        const string updateName = "pupdate.zip";
        string updateLocation = Path.Combine(directory, updateName);

        int exitCode = int.MinValue;

        try
        {
            // Load System.IO.Compression now
            Assembly.Load("System.IO.Compression");

            if (SYSTEM_OS_PLATFORM != "win")
            {
                Assembly.Load("System.IO.Pipes");
            }

            // Move current process file
            Console.WriteLine($"Renaming {execLocation} to {backupLocation}");
            File.Move(execLocation, backupLocation, true);

            // Extract update
            Console.WriteLine($"Extracting {updateLocation} to {directory}");
            ZipFile.ExtractToDirectory(updateLocation, directory, true);

            // Execute
            Console.WriteLine($"Executing {execLocation}");

            ProcessStartInfo pInfo = new ProcessStartInfo(execLocation)
            {
                Arguments = string.Join(' ', updaterArgs),
                UseShellExecute = false
            };

            Process p = Process.Start(pInfo);

            p.WaitForExit();

            exitCode = p.ExitCode;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"An error occurred: {e.GetType().Name}:{e}");
        }

        return exitCode;
    }
}
