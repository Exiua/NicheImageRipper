using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ResourceIdentifier
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}