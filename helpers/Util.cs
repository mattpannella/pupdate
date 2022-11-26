namespace pannella.analoguepocket;

public class Util
{
    private static string _platformsDirectory = "Platforms";
    private static string _temp = "imagesbackup";
    public static bool BackupPlatformsDirectory(string rootPath)
    {
        string fullPath = Path.Combine(rootPath, _platformsDirectory);
        if(!Directory.Exists(fullPath)) {
            return false;
        }

        string tempPath = Path.Combine(rootPath, _temp);
        try {
            Directory.CreateDirectory(tempPath);
            CopyDirectory(fullPath, tempPath, true, true);
        } catch (Exception) {
            return false;
        }
        return true;
    }

    public static bool RestorePlatformsDirectory(string rootPath)
    {
        string fullPath = Path.Combine(rootPath, _platformsDirectory);
        if(!Directory.Exists(fullPath)) {
            return false;
        }
        string tempPath = Path.Combine(rootPath, _temp);
        if(!Directory.Exists(tempPath)) {
            return false;
        }
        CopyDirectory(tempPath, fullPath, true, true);
        Directory.Delete(tempPath, true);

        return true;
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
}