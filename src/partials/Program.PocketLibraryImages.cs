using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Services;
using ArchiveFile = Pannella.Models.Archive.File;

namespace Pannella;

internal partial class Program
{
    private static void DownloadPockLibraryImages(string directory)
    {
        const string fileName = "Library_Image_Set_v1.0.zip";
        ArchiveFile archiveFile = GlobalHelper.ArchiveFiles.GetFile(fileName);

        if (archiveFile != null)
        {
            string localFile = Path.Combine(directory, fileName);
            string extractPath = Path.Combine(directory, "temp");

            try
            {
                ArchiveService.DownloadArchiveFile(GlobalHelper.SettingsManager.GetConfig().archive_name,
                    archiveFile, directory);
                Console.WriteLine("Installing...");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);
                Util.CopyDirectory(extractPath, directory, true, true);

                Directory.Delete(extractPath, true);
                Console.WriteLine("Complete.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Something happened while trying to install the asset files...");
#if DEBUG
                Console.WriteLine(e);
#else
                Console.WriteLine(e.Message);
#endif
            }
        }
    }
}
