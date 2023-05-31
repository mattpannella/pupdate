using System.IO;
using Force.Crc32;
using System.Security.Cryptography;
namespace pannella.analoguepocket;

public class Util
{
    private static string _platformsDirectory = "Platforms";
    private static string _temp = "imagesbackup";
    private static readonly string[] BAD_DIRS = { "__MACOSX" };
    public enum HashTypes {
        CRC32,
        MD5
    }

    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool overwrite)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

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
        foreach(var dir in BAD_DIRS) {
            try {
                Directory.Delete(Path.Combine(source, dir), true);
            }
            catch { }
        }

        if(preservePlatformsFolder) {
            string existing = Path.Combine(Factory.GetGlobals().UpdateDirectory, _platformsDirectory, platform + ".json");
            if(File.Exists(existing)) {
                try {
                    string dir = Path.Combine(source, _platformsDirectory);
                    Directory.Delete(dir, true);
                }
                catch { }
            }
        }

        // Clean files.
        var files = Directory.EnumerateFiles(source).Where(file => isBadFile(Path.GetFileName(file)));
        foreach(var file in files) {
            try {
                File.Delete(file);
            }
            catch { }
        }

        // Recurse through subdirectories.
        var dirs = Directory.GetDirectories(source);
        foreach(var dir in dirs) {
            CleanDir(Path.Combine(source, Path.GetFileName(dir)));
        }

        static bool isBadFile(string name)
        {
            if (name.StartsWith('.')) return true;
            if (name.EndsWith(".mra")) return true;
            if (name.EndsWith(".txt")) return true;
            return false;
        }
    }

    public static string GetCRC32(string filepath)
    {
        if(File.Exists(filepath)) {
            var checksum = Crc32Algorithm.Compute(File.ReadAllBytes(filepath));
            return checksum.ToString("x8");
        } else {
            throw new Exception("File doesn't exist. Cannot compute checksum");
        }
    }

    public static string GetMD5(string filepath)
    {
        if(File.Exists(filepath)) {
            var checksum = MD5.HashData(File.ReadAllBytes(filepath));
            return Convert.ToHexString(checksum);
        } else {
            throw new Exception("File doesn't exist. Cannot compute checksum");
        }
    }

    public static bool CompareChecksum(string filepath, string checksum, HashTypes type = HashTypes.CRC32)
    {
        string hash;
        switch(type) {
            case HashTypes.MD5:
                hash = GetMD5(filepath);
                break;
            case HashTypes.CRC32:
            default:
                hash = GetCRC32(filepath);
                break;
        }
        
        return hash.Equals(checksum, StringComparison.CurrentCultureIgnoreCase);
    }
    
}
