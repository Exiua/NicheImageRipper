using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class ContentUnlockOptionAttributes
{
    [JsonPropertyName("content_unlock_type")]
    public string? ContentUnlockType { get; set; }

    [JsonPropertyName("is_current_user_eligible")]
    public bool IsCurrentUserEligible { get; set; }

    [JsonPropertyName("reward_benefit_categories")]
    public List<string> RewardBenefitCategories { get; set; } = [];
}