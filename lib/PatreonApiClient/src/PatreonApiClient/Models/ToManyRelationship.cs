using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ToManyRelationship
{
    [JsonPropertyName("data")]
    public List<ResourceIdentifier> Data { get; set; } = [];

    [JsonPropertyName("links")]
    public RelatedLink? Links { get; set; }
}