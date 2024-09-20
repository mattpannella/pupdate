using System.Security.Cryptography;
using System.Text;
using Force.Crc32;

namespace Pannella.Helpers;

public class Util
{
    private const string PLATFORMS_DIRECTORY = "Platforms";

    private static readonly string[] BAD_DIRS = { "__MACOSX" };

    public enum HashTypes
    {
        CRC32,
        MD5
    }

    public static int CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool overwrite,
        int currentFileCount = 0, int? totalFiles = null)
    {
        bool console = false;

        try
        {
            _ = Console.WindowWidth;
            console = true;
        }
        catch
        {
            // Ignore
        }

        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        List<string> allFiles = Directory.GetFiles(sourceDir, "*",SearchOption.AllDirectories).ToList();

        int total = totalFiles ?? allFiles.Count;
        int count = currentFileCount;

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);

            file.CopyTo(targetFilePath, overwrite);

            count++;

            if (console)
                ConsoleHelper.ShowProgressBar(count, total);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);

                count = CopyDirectory(subDir.FullName, newDestinationDir, true, overwrite, count, total);
            }
        }

        return count;
    }

    public static void CleanDir(string source, string path = "", bool preservePlatformsFolder = false, string platform = "")
    {
        // Clean up any bad directories (like Mac OS directories).
        foreach (var dir in BAD_DIRS)
        {
            try
            {
                Directory.Delete(Path.Combine(source, dir), true);
            }
            catch
            {
                // Ignore
            }
        }

        if (preservePlatformsFolder)
        {
            string existing = Path.Combine(path, PLATFORMS_DIRECTORY, platform + ".json");

            if (File.Exists(existing))
            {
                try
                {
                    string dir = Path.Combine(source, PLATFORMS_DIRECTORY);

                    Directory.Delete(dir, true);
                }
                catch
                {
                    // Ignore
                }
            }
        }

        // Clean files.
        var files = Directory.EnumerateFiles(source).Where(file => IsBadFile(Path.GetFileName(file)));

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore
            }
        }

        // Recurse through subdirectories.
        var dirs = Directory.GetDirectories(source);

        foreach (var dir in dirs)
        {
            CleanDir(Path.Combine(source, Path.GetFileName(dir)));
        }
    }

    private static bool IsBadFile(string name)
    {
        return name.StartsWith('.') || name.EndsWith(".mra") || name.EndsWith(".txt");
    }

    public static bool CompareChecksum(string filepath, string checksum, HashTypes type = HashTypes.CRC32)
    {
        if (!File.Exists(filepath))
        {
            throw new Exception("File doesn't exist. Cannot compute checksum.");
        }

        string hash;

        switch (type)
        {
            case HashTypes.MD5:
            {
                var fileBytes = File.ReadAllBytes(filepath);
                var newChecksum = MD5.HashData(fileBytes);

                hash = Convert.ToHexString(newChecksum);
                // ReSharper disable once RedundantAssignment
                fileBytes = null;
                break;
            }

            case HashTypes.CRC32:
            default:
            {
                var fileBytes = File.ReadAllBytes(filepath);
                var newChecksum = Crc32Algorithm.Compute(fileBytes);

                hash = newChecksum.ToString("x8");
                // ReSharper disable once RedundantAssignment
                fileBytes = null;
                break;
            }
        }

        return hash.Equals(checksum, StringComparison.CurrentCultureIgnoreCase);
    }

    public static string WordWrap(string line, int width, string padding = "")
    {
        string[] parts = line.Split(' ');
        StringBuilder message = new();
        int length = 0;

        message.Append(padding);

        foreach (var part in parts)
        {
            if (length + part.Length + 1 > width)
            {
                message.AppendLine();
                message.Append(padding);
                length = 0;
            }
            else
            {
                length += part.Length + 1;
            }

            message.Append($"{part} ");
        }

        return message.ToString();
    }
}
