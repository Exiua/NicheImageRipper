using System.Text.Json.Serialization;

namespace NicheImageRipper.Core.Clients.Iwara.Models;

public class PagedResponse<T>
{
    [JsonPropertyName("count")]
    public required int Count { get; set; }
    [JsonPropertyName("limit")]
    public required int Limit { get; set; }
    [JsonPropertyName("page")]
    public required int Page { get; set; }
    [JsonPropertyName("results")]
    public required List<T> Results { get; set; }
}