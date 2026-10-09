using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class AccessRuleAttributes
{
    [JsonPropertyName("access_rule_type")]
    public string? AccessRuleType { get; set; }

    [JsonPropertyName("amount_cents")]
    public int? AmountCents { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("post_count")]
    public int PostCount { get; set; }
}