using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixivApi.Models.Converters;

public class ArrayOrObjectJsonConverter<T> : JsonConverter<IReadOnlyCollection<T>>
{
    public override IReadOnlyCollection<T>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize<T[]>(ref reader, options),
            JsonTokenType.StartObject => JsonSerializer.Deserialize<Wrapper>(ref reader, options)?.Items,
            _ => throw new JsonException()
        };

    public override void Write(Utf8JsonWriter writer, IReadOnlyCollection<T> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize<object?>(writer, value, options);

    private record Wrapper(T[] Items);
}