using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ToOneRelationship
{
    [JsonPropertyName("data")]
    public ResourceIdentifier? Data { get; set; }

    [JsonPropertyName("links")]
    public RelatedLink? Links { get; set; }
}