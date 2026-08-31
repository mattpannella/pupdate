// ReSharper disable InconsistentNaming

using Newtonsoft.Json;

namespace Pannella.Models.AiCheck;

public class AiCheckEntry
{
    public double overall_score { get; set; }

    /// <summary>When the report last analyzed this core (Unix epoch milliseconds).</summary>
    public long last_run { get; set; }

    /// <summary>Check category ("CommitsCheck", "ReadmeCheck", "ContributorsCheck") to its findings.
    /// Categories are always present in the report but are usually empty.</summary>
    public Dictionary<string, List<AiCheckResult>> results { get; set; } = new();

    /// <summary>Every finding across all categories, tagged with the category it came from.</summary>
    public IEnumerable<(string Category, AiCheckResult Result)> AllResults =>
        results?
            .Where(category => category.Value != null)
            .SelectMany(category => category.Value
                .Where(result => result != null)
                .Select(result => (category.Key, result)))
        ?? Enumerable.Empty<(string, AiCheckResult)>();
}

public class AiCheckResult
{
    public string name { get; set; }

    public AiCheckScore score { get; set; }

    public List<string> output { get; set; } = new();
}

/// <summary>
/// A single check's verdict. The report writes this either as a bare string ("GuaranteeHuman") or as
/// a one-property object carrying a value ({"SuspectedAi": 0.97}), so it needs its own converter.
/// </summary>
[JsonConverter(typeof(AiCheckScoreConverter))]
public class AiCheckScore
{
    public string label { get; set; }

    public double? value { get; set; }

    public override string ToString()
    {
        string text = label switch
        {
            "GuaranteeHuman" => "human",
            "SuspectedAi" => "suspected AI",
            null or "" => "unknown",
            _ => label
        };

        return value.HasValue ? $"{text} {Math.Round(value.Value * 100)}%" : text;
    }
}

public class AiCheckScoreConverter : JsonConverter<AiCheckScore>
{
    public override AiCheckScore ReadJson(JsonReader reader, Type objectType, AiCheckScore existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                return null;

            case JsonToken.String:
                return new AiCheckScore { label = (string)reader.Value };

            case JsonToken.StartObject:
            {
                var entry = Newtonsoft.Json.Linq.JObject.Load(reader).Properties().FirstOrDefault();

                return entry == null
                    ? null
                    : new AiCheckScore { label = entry.Name, value = entry.Value.ToObject<double?>() };
            }

            default:
                // A bare number, or anything unexpected - keep the value, drop the label.
                return new AiCheckScore { value = serializer.Deserialize<double?>(reader) };
        }
    }

    public override void WriteJson(JsonWriter writer, AiCheckScore value, JsonSerializer serializer) =>
        throw new NotSupportedException();
}
