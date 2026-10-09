using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ResponseMeta
{
    [JsonPropertyName("pagination")]
    public PaginationMeta? Pagination { get; set; }
}