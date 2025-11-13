using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.JsonConverters;

public class StringOrArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return [reader.GetString()!];
            case JsonTokenType.StartArray:
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        list.Add(reader.GetString()!);
                    }
                    else
                    {
                        throw new JsonException("Expected string element in array.");
                    }
                }
                return list.ToArray();
            }
            case JsonTokenType.None:
            case JsonTokenType.StartObject:
            case JsonTokenType.EndObject:
            case JsonTokenType.EndArray:
            case JsonTokenType.PropertyName:
            case JsonTokenType.Comment:
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
            default:
                throw new JsonException("Expected string or array of strings.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var s in value)
        {
            writer.WriteStringValue(s);
        }
        
        writer.WriteEndArray();
    }
}