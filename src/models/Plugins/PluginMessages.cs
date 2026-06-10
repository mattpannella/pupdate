// JSON shape mirrors the serde "externally-tagged" enum format used by the
// pocket-plugin Rust crate. Object variants: {"Choice":{...}} / {"Text":{...}}
// / {"Answer":{...}}. Unit variants: "Exit" / "Kill" as bare JSON strings.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Pannella.Models.Plugins;

public abstract class PluginMessage { }

public class ChoicePluginMessage : PluginMessage
{
    public string Name { get; set; }
    public string Query { get; set; }
    public List<string> Choices { get; set; }
}

public class TextPluginMessage : PluginMessage
{
    public string Name { get; set; }
    public string Query { get; set; }
}

public class ExitPluginMessage : PluginMessage { }

public abstract class HostMessage { }

public class AnswerHostMessage : HostMessage
{
    public string Name { get; set; }
    public string Value { get; set; }
}

public class KillHostMessage : HostMessage { }

public class PluginMessageConverter : JsonConverter<PluginMessage>
{
    public override PluginMessage ReadJson(JsonReader reader, Type objectType, PluginMessage existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var name = (string)reader.Value;
            if (name == "Exit")
                return new ExitPluginMessage();
            throw new JsonSerializationException($"Unknown PluginMessage unit variant: {name}");
        }

        var token = JToken.ReadFrom(reader);
        if (token is not JObject obj || obj.Properties().Count() != 1)
            throw new JsonSerializationException("PluginMessage must be a single-property object or a unit string");

        var prop = obj.Properties().Single();
        var inner = (JObject)prop.Value;

        return prop.Name switch
        {
            "Choice" => new ChoicePluginMessage
            {
                Name = (string)inner["name"],
                Query = (string)inner["query"],
                Choices = inner["choices"]?.ToObject<List<string>>() ?? new List<string>(),
            },
            "Text" => new TextPluginMessage
            {
                Name = (string)inner["name"],
                Query = (string)inner["query"],
            },
            _ => throw new JsonSerializationException($"Unknown PluginMessage variant: {prop.Name}"),
        };
    }

    public override void WriteJson(JsonWriter writer, PluginMessage value, JsonSerializer serializer)
    {
        throw new NotSupportedException("Host does not produce PluginMessage values");
    }
}

public class HostMessageConverter : JsonConverter<HostMessage>
{
    public override HostMessage ReadJson(JsonReader reader, Type objectType, HostMessage existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotSupportedException("Host does not consume HostMessage values");
    }

    public override void WriteJson(JsonWriter writer, HostMessage value, JsonSerializer serializer)
    {
        switch (value)
        {
            case KillHostMessage:
                writer.WriteValue("Kill");
                return;
            case AnswerHostMessage a:
                writer.WriteStartObject();
                writer.WritePropertyName("Answer");
                writer.WriteStartObject();
                writer.WritePropertyName("name");
                writer.WriteValue(a.Name);
                writer.WritePropertyName("value");
                writer.WriteValue(a.Value);
                writer.WriteEndObject();
                writer.WriteEndObject();
                return;
            default:
                throw new JsonSerializationException($"Unknown HostMessage variant: {value?.GetType().Name ?? "null"}");
        }
    }
}
