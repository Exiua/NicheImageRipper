using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class AuthorAttributes
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}