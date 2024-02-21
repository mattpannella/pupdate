using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Analogue;

namespace Pannella.Services;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
public class FirmwareService : BaseService
{
    private const string BASE_URL = "https://www.analogue.co/";
    private const string DETAILS = "support/pocket/firmware/{0}/details";
    private const string FILENAME_PATTERN = "pocket_firmware_*.bin";

    private static ReleaseDetails latest;

    private static async Task<ReleaseDetails> GetDetails(string version = "latest")
    {
        if (latest != null)
        {
            return latest;
        }

        string url = string.Format(BASE_URL + DETAILS, version);
        string response = await HttpHelper.Instance.GetHTML(url);
        ReleaseDetails details = JsonSerializer.Deserialize<ReleaseDetails>(response);

        if (version == "latest")
        {
            latest = details;
        }

        return details;
    }

    public async Task<string> UpdateFirmware()
    {
        string version = string.Empty;

        WriteMessage("Checking for firmware updates...");

        var details = await GetDetails();
        string[] parts = details.download_url.Split("/");
        string filename = parts[parts.Length - 1];
        string filepath = Path.Combine(GlobalHelper.UpdateDirectory, filename);

        if (!File.Exists(filepath) || !Util.CompareChecksum(filepath, details.md5, Util.HashTypes.MD5))
        {
            version = filename;

            var oldFiles = Directory.GetFiles(GlobalHelper.UpdateDirectory, FILENAME_PATTERN);

            WriteMessage("Firmware update found. Downloading...");

            await HttpHelper.Instance.DownloadFileAsync(details.download_url, Path.Combine(GlobalHelper.UpdateDirectory, filename));

            WriteMessage("Download Complete.");
            WriteMessage(Path.Combine(GlobalHelper.UpdateDirectory, filename));

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
