using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PaginationCursors
{
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}