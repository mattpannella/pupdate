using Newtonsoft.Json;

namespace Pannella.Models.Analogue.Video;

public class DisplayMode
{
    public string id { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string description { get; set; }

    public override string ToString()
    {
        return $"{this.id} {this.description}";
    }
}
