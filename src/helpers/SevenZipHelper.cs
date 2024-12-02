using Aspose.Zip.SevenZip;

namespace Pannella.Helpers;

public class SevenZipHelper
{
    public static void ExtractToDirectory(string zipFile, string destination)
    {
        SevenZipArchive sevenzip = new SevenZipArchive(zipFile);
        Console.WriteLine("Extracting...");
        sevenzip.ExtractToDirectory(destination);
    }
}