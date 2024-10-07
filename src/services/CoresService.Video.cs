using Newtonsoft.Json;
using Pannella.Models.Analogue.Video;
using AnalogueDisplayMode = Pannella.Models.Analogue.Video.DisplayMode;
using DisplayMode = Pannella.Models.DisplayModes.DisplayMode;

namespace Pannella.Services;

public partial class CoresService
{
    public const int DISPLAY_MODES_MAX = 16;

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

    public void AddDisplayModes(string identifier, List<DisplayMode> displayModes = null, bool isCurated = false,
        bool forceOriginal = false, bool merge = false)
    {
        var info = this.ReadCoreJson(identifier);
        var video = this.ReadVideoJson(identifier);
        Dictionary<string, DisplayMode> toAdd = new Dictionary<string, DisplayMode>();

        if (isCurated)
        {
            if (info.metadata.platform_ids.Contains("gb") && this.DisplayModes.TryGetValue("gb", out var gb))
            {
                foreach (var displayMode in gb.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
            else if (info.metadata.platform_ids.Contains("gbc") && this.DisplayModes.TryGetValue("gbc", out var gbc))
            {
                foreach (var displayMode in gbc.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
            else if (info.metadata.platform_ids.Contains("gba") && this.DisplayModes.TryGetValue("gba", out var gba))
            {
                foreach (var displayMode in gba.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
            else if (info.metadata.platform_ids.Contains("gg") && this.DisplayModes.TryGetValue("gg", out var gg))
            {
                foreach (var displayMode in gg.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }

            }
            else if (info.metadata.platform_ids.Contains("lynx") && this.DisplayModes.TryGetValue("lynx", out var lynx))
            {
                foreach (var displayMode in lynx.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
            else if (info.metadata.platform_ids.Contains("jtngpc") && this.DisplayModes.TryGetValue("jtngpc", out var ngpc))
            {
                foreach (var displayMode in ngpc.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
            else if (info.metadata.platform_ids.Contains("jtngp") && this.DisplayModes.TryGetValue("jtngp", out var ngp))
            {
                foreach (var displayMode in ngp.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
            else if (info.metadata.platform_ids.Contains("pce") && this.DisplayModes.TryGetValue("pce", out var pce))
            {
                foreach (var displayMode in pce.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }

            if (this.DisplayModes.TryGetValue("all", out var all))
            {
                foreach (var displayMode in all.Where(dm => !dm.exclude_cores.Contains(identifier)))
                {
                    toAdd.TryAdd(displayMode.value, displayMode);
                }
            }
        }
        else
        {
            displayModes ??= this.AllDisplayModes.Where(dm => dm.exclude_cores.Contains(identifier)).ToList();

            foreach (var displayMode in displayModes)
            {
                toAdd.TryAdd(displayMode.value, displayMode);
            }
        }

        var settings = this.settingsService.GetCoreSettings(identifier);

        if (!settings.display_modes || forceOriginal)
        {
            settings.original_display_modes = video.display_modes is { Count: > 0 }
                ? string.Join(',', video.display_modes.Select(d => d.id))
                : string.Empty;
        }

        if (merge && video.display_modes is { Count: > 0 })
        {
            var convertedVideoDisplayModes = this.ConvertDisplayModes(video.display_modes);

            foreach (var displayMode in convertedVideoDisplayModes)
            {
                toAdd.TryAdd(displayMode.value, displayMode);
            }

            if (toAdd.Count > DISPLAY_MODES_MAX)
            {
                WriteMessage($"Unable to merge display modes. Total count is {toAdd.Count} greater than {DISPLAY_MODES_MAX}.");
                return;
            }
        }

        settings.display_modes = true;
        settings.selected_display_modes = string.Join(',', toAdd.Keys);
        video.display_modes = this.settingsService.Config.add_display_mode_description_to_video_json
            ? toAdd.OrderBy(kvp => kvp.Value.order)
                   .Select(kvp => new AnalogueDisplayMode
                                  {
                                      id = kvp.Value.value,
                                      description = kvp.Value.description
                                  })
                   .ToList()
            : toAdd.OrderBy(kvp => kvp.Value.order)
                   .Select(kvp => new AnalogueDisplayMode { id = kvp.Value.value })
                   .ToList();

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);

        File.WriteAllText(Path.Combine(this.installPath, "Cores", identifier, "video.json"), json);
    }

    public void ClearDisplayModes(string identifier)
    {
        var video = this.ReadVideoJson(identifier);

        video.display_modes = null;

        Dictionary<string, Video> output = new Dictionary<string, Video> { { "video", video } };
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);

        File.WriteAllText(Path.Combine(this.installPath, "Cores", identifier, "video.json"), json);
    }
}
