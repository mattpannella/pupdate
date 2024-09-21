using Newtonsoft.Json;
using Pannella.Models.Analogue.Video;

namespace Pannella.Services;

public partial class CoresService
{
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

    public void AddDisplayModes(string identifier, string[] displayModes = null, bool isCurated = false, bool forceOriginal = false)
    {
        var info = this.ReadCoreJson(identifier);
        var video = this.ReadVideoJson(identifier);
        List<DisplayMode> toAdd = new List<DisplayMode>();

        if (isCurated)
        {
            if (info.metadata.platform_ids.Contains("gb") && this.DisplayModes.TryGetValue("gb", out var gb))
            {
                toAdd.AddRange(gb.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("gbc") && this.DisplayModes.TryGetValue("gbc", out var gbc))
            {
                toAdd.AddRange(gbc.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("gba") && this.DisplayModes.TryGetValue("gba", out var gba))
            {
                toAdd.AddRange(gba.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("gg") && this.DisplayModes.TryGetValue("gg", out var gg))
            {
                toAdd.AddRange(gg.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("lynx") && this.DisplayModes.TryGetValue("lynx", out var lynx))
            {
                toAdd.AddRange(lynx.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("jtngpc") && this.DisplayModes.TryGetValue("jtngpc", out var ngpc))
            {
                toAdd.AddRange(ngpc.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("jtngp") && this.DisplayModes.TryGetValue("jtngp", out var ngp))
            {
                toAdd.AddRange(ngp.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
            else if (info.metadata.platform_ids.Contains("pce") && this.DisplayModes.TryGetValue("pce", out var pce))
            {
                toAdd.AddRange(pce.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }

            if (this.DisplayModes.TryGetValue("all", out var all))
            {
                toAdd.AddRange(all.Select(displayMode => new DisplayMode { id = displayMode.value }));
            }
        }
        else
        {
            displayModes ??= this.GetAllDisplayModes().Select(m => m.value).ToArray();
            toAdd = displayModes.Select(id => new DisplayMode { id = id }).ToList();
        }

        var settings = this.settingsService.GetCoreSettings(identifier);

        if (!settings.display_modes || forceOriginal)
        {
            // if this is the first time custom display modes are being applied, save the original ones
            settings.original_display_modes = video.display_modes is { Count: > 0 }
                ? string.Join(',', video.display_modes.Select(d => d.id))
                : string.Empty;
        }

        settings.display_modes = true;
        settings.selected_display_modes = string.Join(',', toAdd.Select(d => d.id));

        video.display_modes = toAdd;

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);

        File.WriteAllText(Path.Combine(this.installPath, "Cores", identifier, "video.json"), json);
    }
}
