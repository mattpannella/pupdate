using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pannella.Models.Settings;

[JsonConverter(typeof(StringEnumConverter))]
public enum ArchiveType
{
    internet_archive,
    custom_archive,
    core_specific_archive,
}

public class Archive
{
    public string name { get; set; }

    public ArchiveType type { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string archive_name { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string archive_folder { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string url { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string index { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public List<string> file_extensions { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool has_instance_jsons { get; set; }

    /// <summary>
    /// This setting only applies to Core Specific Archives
    /// </summary>
    public bool enabled;
}
