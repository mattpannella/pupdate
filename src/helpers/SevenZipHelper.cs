using Aspose.Zip.SevenZip;

namespace Pannella.Helpers;

public static class SevenZipHelper
{
    public static void ExtractToDirectory(string zipFile, string destination)
    {
        SevenZipArchive sevenZip = new SevenZipArchive(zipFile);
        
        Console.WriteLine("Extracting...");
        
        sevenZip.ExtractToDirectory(destination);
    }
}