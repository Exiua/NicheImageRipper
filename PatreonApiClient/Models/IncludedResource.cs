using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

/// <summary>
/// A generic JSON:API resource from the "included" array. Attributes/relationships are kept as raw
/// JSON since the shape depends on <see cref="Type"/> (campaign, user, media, access-rule, etc.) —
/// use <see cref="GetAttributes{T}"/> against one of the typed attribute classes below once Type is known.
/// </summary>
public sealed class IncludedResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("attributes")]
    public JsonElement Attributes { get; set; }

    [JsonPropertyName("relationships")]
    public JsonElement? Relationships { get; set; }

    public T? GetAttributes<T>() =>
        Attributes.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? default
            : Attributes.Deserialize<T>();
}