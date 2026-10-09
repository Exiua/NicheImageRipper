using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class RewardAttributes
{
    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("amount_cents")]
    public int AmountCents { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("declined_patron_count")]
    public int DeclinedPatronCount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("discord_role_ids")]
    public List<string> DiscordRoleIds { get; set; } = [];

    [JsonPropertyName("edited_at")]
    public DateTimeOffset? EditedAt { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("is_free_tier")]
    public bool IsFreeTier { get; set; }

    [JsonPropertyName("patron_amount_cents")]
    public int PatronAmountCents { get; set; }

    [JsonPropertyName("patron_currency")]
    public string? PatronCurrency { get; set; }

    [JsonPropertyName("post_count")]
    public int PostCount { get; set; }

    [JsonPropertyName("published")]
    public bool Published { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("remaining")]
    public int? Remaining { get; set; }

    [JsonPropertyName("requires_shipping")]
    public bool RequiresShipping { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("unpublished_at")]
    public DateTimeOffset? UnpublishedAt { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    // Only present on some reward entries
    [JsonPropertyName("welcome_message")]
    public string? WelcomeMessage { get; set; }

    [JsonPropertyName("welcome_message_unsafe")]
    public string? WelcomeMessageUnsafe { get; set; }

    [JsonPropertyName("welcome_video_embed")]
    public JsonElement? WelcomeVideoEmbed { get; set; }

    [JsonPropertyName("welcome_video_url")]
    public string? WelcomeVideoUrl { get; set; }
}