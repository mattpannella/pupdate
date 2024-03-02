using System.IO.Compression;
using Pannella.Helpers;
using Pannella.Models.Settings;
using ArchiveFile = Pannella.Models.Archive.File;

namespace Pannella;

internal partial class Program
{
    private static void DownloadPockLibraryImages()
    {
        const string fileName = "Library_Image_Set_v1.0.zip";
        Archive archive = ServiceHelper.ArchiveService.GetArchive();
        ArchiveFile archiveFile = ServiceHelper.ArchiveService.GetArchiveFile(fileName);

        if (archiveFile != null)
        {
            string localFile = Path.Combine(ServiceHelper.UpdateDirectory, fileName);
            string extractPath = Path.Combine(ServiceHelper.UpdateDirectory, "temp");

            try
            {
                ServiceHelper.ArchiveService.DownloadArchiveFile(archive, archiveFile, ServiceHelper.UpdateDirectory);
                Console.WriteLine("Installing...");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(localFile, extractPath);
                File.Delete(localFile);
                Util.CopyDirectory(extractPath, ServiceHelper.UpdateDirectory, true, true);

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
        else
        {
            Console.WriteLine("Pocket Library Images not found in the archive.");
        }
    }
}
