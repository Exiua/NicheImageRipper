using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class CampaignAttributes
{
    [JsonPropertyName("avatar_photo_image_urls")]
    public ImageUrlSet? AvatarPhotoImageUrls { get; set; }

    [JsonPropertyName("avatar_photo_url")]
    public string? AvatarPhotoUrl { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("is_monthly")]
    public bool IsMonthly { get; set; }

    [JsonPropertyName("is_nsfw")]
    public bool IsNsfw { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("patron_count")]
    public int PatronCount { get; set; }

    [JsonPropertyName("primary_theme_color")]
    public string? PrimaryThemeColor { get; set; }

    [JsonPropertyName("show_audio_post_download_links")]
    public bool ShowAudioPostDownloadLinks { get; set; }

    [JsonPropertyName("show_free_membership_cta")]
    public bool ShowFreeMembershipCta { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}