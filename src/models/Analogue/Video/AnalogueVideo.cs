using Newtonsoft.Json;

namespace Pannella.Models.Analogue.Video;

public class Video
{
    public string magic { get; set; }

    public List<ScalerMode> scaler_modes { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public List<DisplayMode> display_modes { get; set; }
}
