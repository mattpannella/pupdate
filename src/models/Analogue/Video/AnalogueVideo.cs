namespace Pannella.Models.Analogue.Video;

public class Video
{
    public string magic { get; set; }

    public List<ScalerMode> scaler_modes { get; set; }

    public List<DisplayMode> display_modes { get; set; }
}
