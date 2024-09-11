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
        "0x40", // Backlit Color LCD
        "0xE0", // Pinball Neon Matrix
        "0xE1", // Vacuum Fluorescent
    };

    private static readonly string[] GB_MODES =
    {
        "0x21", // Original GB DMG
        "0x22", // Original GBP
        "0x23", // Original GBP Light
    };

   private static readonly string[] GBC_MODES =
    {
        "0x31", // Original GBC LCD
        "0x32", // Original GBC LCD+
    };

    private static readonly string[] GBA_MODES =
    {
        "0x41", // Original GBA LCD
        "0x42", // Original GBA SP 101
    };

    private static readonly string[] GG_MODES =
    {
        "0x51", // Original GG
        "0x52", // Original GG+
    };

    private static readonly string[] NGP_MODES =
    {
        "0x61", // Original NGP
    };

    private static readonly string[] NGPC_MODES =
    {
        "0x62", // Original NGPC
        "0x63", // Original NGPC+
    };

    private static readonly string[] PCE_MODES =
    {
        "0x71", // TurboExpress
        "0x72", // PC Engine LT
    };

    private static readonly string[] LYNX_MODES =
    {
        "0x81", // Original Lynx
        "0x82", // Original Lynx+
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
        List<DisplayMode> all = new List<DisplayMode>();

        switch (info.metadata.platform_ids)

        {
            case string[] p when p.Contains("gb"):
                all.AddRange(GB_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Contains("gbc"):
                all.AddRange(GBC_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Contains("gba"):
                all.AddRange(GBA_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Contains("gg"):
                all.AddRange(GG_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Contains("lynx"):
                all.AddRange(LYNX_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Any(s => s.EndsWith("ngpc")): // ngpc & jtngpc
                all.AddRange(NGPC_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Any(s => s.EndsWith("ngp")): // ngp & jtngp
                all.AddRange(NGP_MODES.Select(id => new DisplayMode { id = id }));
                break;
            case string[] p when p.Any(s => s.StartsWith("pce")): // pce & pcecd
                all.AddRange(PCE_MODES.Select(id => new DisplayMode { id = id }));
                break;

        }

        all.AddRange(ALL_MODES.Select(id => new DisplayMode { id = id }));
        video.display_modes = all;

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);

        File.WriteAllText(Path.Combine(this.installPath, "Cores", identifier, "video.json"), json);
    }
}
