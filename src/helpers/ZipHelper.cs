using System.IO.Compression;

namespace Pannella.Helpers;

public class ZipHelper
{
    public Progress<ZipProgress> _progress;

    private static void Report(object sender, ZipProgress zipProgress)
    {
        var progress = (double)zipProgress.Processed / (double)zipProgress.Total;

        var progressWidth = Console.WindowWidth - 14;
        var progressBarWidth = (int)(progress * progressWidth);
        var progressBar = new string('=', progressBarWidth);
        var emptyProgressBar = new string(' ', progressWidth - progressBarWidth);

        Console.Write($"\r{progressBar}{emptyProgressBar}] {(progress * 100):0.00}%");

        if (zipProgress.Processed == zipProgress.Total)
        {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.WindowWidth));
            Console.CursorLeft = 0;
            Console.Write("\r");
        }
    }

    public static void ExtractToDirectory(string zipFile, string destination, bool overwrite = false)
    {
        Progress<ZipProgress> _progress = new Progress<ZipProgress>();
        _progress.ProgressChanged += Report;
        var stream = new FileStream(zipFile, FileMode.Open);
        var zip = new ZipArchive(stream);
        zip.ExtractToDirectory(destination, _progress, overwrite);
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
    public static void ExtractToDirectory(this ZipArchive zipFile, string target, IProgress<ZipProgress> progress)
    {
        ExtractToDirectory(zipFile, target, progress, overwrite: false);
    }

    public static void ExtractToDirectory(this ZipArchive zipFile, string target, IProgress<ZipProgress> progress, bool overwrite)
    {
        if (zipFile == null)
        {
            throw new ArgumentNullException(nameof(zipFile));
        }
            
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
            
        DirectoryInfo info = Directory.CreateDirectory(target);
        string destinationDirectoryFullPath = info.FullName;

        int count = 0;
        foreach (ZipArchiveEntry entry in zipFile.Entries)
        {
            count++;
            string fileDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, entry.FullName));

            if (!fileDestinationPath.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
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
                Directory.CreateDirectory(Path.GetDirectoryName(fileDestinationPath));
                entry.ExtractToFile(fileDestinationPath, overwrite: overwrite);
            }
        }
    }
}