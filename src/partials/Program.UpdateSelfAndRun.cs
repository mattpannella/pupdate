using System.Diagnostics;
using System.Reflection;
using System.Text;
using Pannella.Helpers;

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
            Assembly.Load("System.IO.Compression.ZipFile");

            if (SYSTEM_OS_PLATFORM != "win")
            {
                Assembly.Load("System.IO.Pipes");
            }

            // Move current process file
            Console.WriteLine($"Renaming {execLocation} to {backupLocation}");
            File.Move(execLocation, backupLocation, true);

            // Extract update
            Console.WriteLine($"Extracting {updateLocation} to {directory}");
            ZipHelper.ExtractToDirectory(updateLocation, directory, true);

            // Execute
            Console.WriteLine($"Executing {execLocation}");

            // Rebuild the arguments, quoting the values
            // First element is always the verb, quote every element
            //   that starts with a dash (denoting a switch)
            StringBuilder args = new();

            for (int i = 0; i < updaterArgs.Length; i++)
            {
                if (i != 0 && updaterArgs[i][0] != '-')
                    args.Append($"\"{updaterArgs[i]}\" ");
                else
                    args.Append($"{updaterArgs[i]} ");
            }

            ProcessStartInfo pInfo = new ProcessStartInfo(execLocation)
            {
                Arguments = args.ToString(),
                UseShellExecute = false
            };

            Process p = Process.Start(pInfo);

            p!.WaitForExit();

            exitCode = p.ExitCode;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"An error occurred: {e.GetType().Name}:{e}");
        }

        return exitCode;
    }
}
