using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class UserAttributes
{
    [JsonPropertyName("link_url")]
    public string? LinkUrl { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}