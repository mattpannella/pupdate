using System.IO.Compression;

namespace Pannella.Helpers;

public class ZipHelper
{
    private static void UpdateProgress(object sender, ZipProgress zipProgress)
    {
        try
        {
            _ = Console.WindowWidth;
            ConsoleHelper.ShowProgressBar(zipProgress.Processed, zipProgress.Total);
        }
        catch
        {
            // Ignore
        }
    }

    public static void ExtractToDirectory(string zipFile, string destination, bool overwrite = false, bool useProgress = true)
    {
        Progress<ZipProgress> progress = new Progress<ZipProgress>();

        if (useProgress)
        {
            progress.ProgressChanged += UpdateProgress;
        }

        var stream = new FileStream(zipFile, FileMode.Open);
        var zip = new ZipArchive(stream);

        zip.ExtractToDirectory(destination, progress, overwrite);
        stream.Close();
    }
}

public class ZipProgress
{
    public ZipProgress(int total, int processed, string currentItem)
    {
        Total = total;
        Processed = processed;
        CurrentItem = currentItem;
    }

    public int Total { get; }
    public int Processed { get; }
    public string CurrentItem { get; }
}

public static class ZipExtension
{
    public static void ExtractToDirectory(this ZipArchive zipFile, string target, IProgress<ZipProgress> progress, bool overwrite = false)
    {
        DirectoryInfo info = Directory.CreateDirectory(target);
        string targetPath = info.FullName;
        int count = 0;

        foreach (ZipArchiveEntry entry in zipFile.Entries)
        {
            count++;

            string fileDestinationPath = Path.GetFullPath(Path.Combine(targetPath, entry.FullName));

            if (!fileDestinationPath.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("File is extracting to outside of the folder specified.");
            }

            var zipProgress = new ZipProgress(zipFile.Entries.Count, count, entry.FullName);

            progress.Report(zipProgress);

            if (Path.GetFileName(fileDestinationPath).Length == 0)
            {
                if (entry.Length != 0)
                {
                    throw new IOException("Directory entry with data.");
                }

                Directory.CreateDirectory(fileDestinationPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fileDestinationPath)!);
                entry.ExtractToFile(fileDestinationPath, overwrite: overwrite);
            }
        }
    }
}
