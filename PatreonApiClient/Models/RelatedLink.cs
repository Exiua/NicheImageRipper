using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class RelatedLink
{
    [JsonPropertyName("related")]
    public string? Related { get; set; }
}