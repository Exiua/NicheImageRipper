using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PaginationLinks
{
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}