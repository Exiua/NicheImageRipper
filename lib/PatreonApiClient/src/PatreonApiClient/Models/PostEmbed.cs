using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostEmbed
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("linked_object_id")]
    public string? LinkedObjectId { get; set; }

    [JsonPropertyName("linked_object_type")]
    public string? LinkedObjectType { get; set; }

    [JsonPropertyName("product_variant_id")]
    public string? ProductVariantId { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("provider_url")]
    public string? ProviderUrl { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}