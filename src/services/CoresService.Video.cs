using Newtonsoft.Json;
using Pannella.Models.Analogue.Video;

namespace Pannella.Services;

public partial class CoresService
{
    private static readonly string[] ALL_MODES =
    {
        "0x10", // CRT Trinitron
        "0x20", // Grayscale LCD
        "0x30", // Reflective Color LCD
        "0x31", // Original GBC LCD
        "0x32", // Original GBC LCD+
        "0x40", // Backlit Color LCD
        "0x41", // Original GBA LCD
        "0x42", // Original GBA SP 101
        "0x51", // Original GG
        "0x52", // Original GG+
        "0x61", // Original NGP
        "0x62", // Original NGPC
        "0x63", // Original NGPC+
        "0x71", // TurboExpress
        "0x72", // PC Engine LT
        "0x81", // Original Lynx
        "0x82", // Original Lynx+
        "0xE0", // Pinball Neon Matrix
        "0xE1", // Vacuum Fluorescent
    };

    private static readonly string[] GB_MODES =
    {
        "0x21", // Original GB DMG
        "0x22", // Original GBP
        "0x23", // Original GBP Light
    };

    public void ChangeAspectRatio(string identifier, int fromWidth, int fromHeight, int toWidth, int toHeight)
    {
        var video = this.ReadVideoJson(identifier);

        foreach (var scalarMode in video.scaler_modes)
        {
            if (scalarMode.aspect_w == fromWidth && scalarMode.aspect_h == fromHeight)
            {
                scalarMode.aspect_w = toWidth;
                scalarMode.aspect_h = toHeight;
            }
        }

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);

        File.WriteAllText(Path.Combine(this.installPath, "Cores", identifier, "video.json"), json);
    }

    public void AddDisplayModes(string identifier)
    {
        var info = this.ReadCoreJson(identifier);
        var video = this.ReadVideoJson(identifier);
        List<DisplayMode> all = ALL_MODES.Select(id => new DisplayMode { id = id }).ToList();

        if (info.metadata.platform_ids.Contains("gb"))
        {
            all.AddRange(GB_MODES.Select(id => new DisplayMode { id = id }));
        }

        video.display_modes = all;

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);

        File.WriteAllText(Path.Combine(this.installPath, "Cores", identifier, "video.json"), json);
    }
}
