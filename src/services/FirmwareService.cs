using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Analogue;

namespace Pannella.Services;

public class FirmwareService : Base
{
    private const string BASE_URL = "https://www.analogue.co/";
    private const string DETAILS = "support/pocket/firmware/{0}/details";
    private const string FILENAME_PATTERN = "pocket_firmware_*.bin";

    private static ReleaseDetails latest;

    private static ReleaseDetails GetDetails(string version = "latest")
    {
        if (latest != null)
        {
            return latest;
        }

        string url = string.Format(BASE_URL + DETAILS, version);
        string response = HttpHelper.Instance.GetHTML(url);
        ReleaseDetails details = JsonConvert.DeserializeObject<ReleaseDetails>(response);

        if (version == "latest")
        {
            latest = details;
        }

        return details;
    }

    public string UpdateFirmware(string path)
    {
        string version = string.Empty;

        WriteMessage("Checking for firmware updates...");

        var details = GetDetails();
        string[] parts = details.download_url.Split("/");
        string filename = parts[parts.Length - 1];
        string filepath = Path.Combine(path, filename);

        if (!File.Exists(filepath) || !Util.CompareChecksum(filepath, details.md5, Util.HashTypes.MD5))
        {
            version = filename;

            var oldFiles = Directory.GetFiles(path, FILENAME_PATTERN);

            WriteMessage("Firmware update found. Downloading...");

            HttpHelper.Instance.DownloadFile(details.download_url, Path.Combine(path, filename));

            WriteMessage("Download Complete.");
            WriteMessage(Path.Combine(path, filename));

            foreach (string oldFile in oldFiles)
            {
                if (File.Exists(oldFile) && Path.GetFileName(oldFile) != filename)
                {
                    WriteMessage("Deleting old firmware file...");
                    File.Delete(oldFile);
                }
            }

            WriteMessage("To install firmware, restart your Pocket.");
        }
        else
        {
            WriteMessage("Firmware up to date.");
        }

        return version;
    }
}
