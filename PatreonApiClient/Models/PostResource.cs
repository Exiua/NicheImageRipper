using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("attributes")]
    public PostAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public PostRelationships? Relationships { get; set; }
}