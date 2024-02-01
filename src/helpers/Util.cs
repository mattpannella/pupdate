using System.Security.Cryptography;
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

    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool overwrite)
    {
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

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);

            file.CopyTo(targetFilePath, overwrite);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);

                CopyDirectory(subDir.FullName, newDestinationDir, true, overwrite);
            }
        }
    }

    public static void CleanDir(string source, bool preservePlatformsFolder = false, string platform = "")
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
            string existing = Path.Combine(GlobalHelper.UpdateDirectory, PLATFORMS_DIRECTORY, platform + ".json");

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
                var newChecksum = MD5.HashData(File.ReadAllBytes(filepath));

                hash = Convert.ToHexString(newChecksum);
                break;
            }

            case HashTypes.CRC32:
            default:
            {
                var newChecksum = Crc32Algorithm.Compute(File.ReadAllBytes(filepath));

                hash = newChecksum.ToString("x8");
                break;
            }
        }

        return hash.Equals(checksum, StringComparison.CurrentCultureIgnoreCase);
    }
}
