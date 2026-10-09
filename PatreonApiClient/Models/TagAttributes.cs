using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class TagAttributes
{
    [JsonPropertyName("entity_id")]
    public long EntityId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}