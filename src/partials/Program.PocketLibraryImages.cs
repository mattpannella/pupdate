using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Services;
using ArchiveFile = Pannella.Models.Archive.File;

namespace Pannella;

internal partial class Program
{
    private static void DownloadPockLibraryImages()
    {
        const string fileName = "Library_Image_Set_v1.0.zip";
        ArchiveFile archiveFile = GlobalHelper.ArchiveService.ArchiveFiles.GetFile(fileName);

        if (archiveFile != null)
        {
            string localFile = Path.Combine(GlobalHelper.UpdateDirectory, fileName);
            string extractPath = Path.Combine(GlobalHelper.UpdateDirectory, "temp");

            try
            {
                GlobalHelper.ArchiveService.DownloadArchiveFile(GlobalHelper.SettingsService.GetConfig().archive_name,
                    archiveFile, GlobalHelper.UpdateDirectory);
                Console.WriteLine("Installing...");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);
                Util.CopyDirectory(extractPath, GlobalHelper.UpdateDirectory, true, true);

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
