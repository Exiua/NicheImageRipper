using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostAttributes
{
    [JsonPropertyName("attachments_preview_metadata")]
    public List<AttachmentPreviewMetadata> AttachmentsPreviewMetadata { get; set; } = [];

    [JsonPropertyName("change_visibility_at")]
    public DateTimeOffset? ChangeVisibilityAt { get; set; }

    [JsonPropertyName("cleaned_teaser_text")]
    public string? CleanedTeaserText { get; set; }

    [JsonPropertyName("comment_count")]
    public int CommentCount { get; set; }

    [JsonPropertyName("commenter_count")]
    public int CommenterCount { get; set; }

    [JsonPropertyName("content_json_string")]
    public string? ContentJsonString { get; set; }

    [JsonPropertyName("content_teaser_text")]
    public string? ContentTeaserText { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("current_user_can_comment")]
    public bool CurrentUserCanComment { get; set; }

    [JsonPropertyName("current_user_can_delete")]
    public bool CurrentUserCanDelete { get; set; }

    [JsonPropertyName("current_user_can_edit")]
    public bool CurrentUserCanEdit { get; set; }

    [JsonPropertyName("current_user_can_report")]
    public bool CurrentUserCanReport { get; set; }

    [JsonPropertyName("current_user_can_view")]
    public bool CurrentUserCanView { get; set; }

    [JsonPropertyName("current_user_comment_disallowed_reason")]
    public string? CurrentUserCommentDisallowedReason { get; set; }

    [JsonPropertyName("current_user_has_liked")]
    public bool CurrentUserHasLiked { get; set; }

    [JsonPropertyName("current_user_has_reshared")]
    public bool CurrentUserHasReshared { get; set; }

    [JsonPropertyName("edited_at")]
    public DateTimeOffset? EditedAt { get; set; }

    // Seen as null and as an object in the sample
    [JsonPropertyName("embed")]
    public PostEmbed? Embed { get; set; }

    [JsonPropertyName("has_custom_thumbnail")]
    public bool HasCustomThumbnail { get; set; }

    [JsonPropertyName("has_ti_violation")]
    public bool HasTiViolation { get; set; }

    [JsonPropertyName("image")]
    public PostImage? Image { get; set; }

    [JsonPropertyName("is_fan_giftable")]
    public bool IsFanGiftable { get; set; }

    [JsonPropertyName("is_new_to_current_user")]
    public bool IsNewToCurrentUser { get; set; }

    [JsonPropertyName("is_paid")]
    public bool IsPaid { get; set; }

    [JsonPropertyName("is_preview_blurred")]
    public bool IsPreviewBlurred { get; set; }

    [JsonPropertyName("like_count")]
    public int LikeCount { get; set; }

    [JsonPropertyName("meta_image_url")]
    public string? MetaImageUrl { get; set; }

    [JsonPropertyName("min_cents_pledged_to_view")]
    public int? MinCentsPledgedToView { get; set; }

    [JsonPropertyName("moderation_status")]
    public string? ModerationStatus { get; set; }

    [JsonPropertyName("patreon_url")]
    public string? PatreonUrl { get; set; }

    [JsonPropertyName("pledge_url")]
    public string? PledgeUrl { get; set; }

    [JsonPropertyName("pls_one_liners_by_category")]
    public List<JsonElement> PlsOneLinersByCategory { get; set; } = [];

    // Seen as null and as an object in the sample
    [JsonPropertyName("post_file")]
    public PostFile? PostFile { get; set; }

    [JsonPropertyName("post_level_suspension_removal_date")]
    public DateTimeOffset? PostLevelSuspensionRemovalDate { get; set; }

    [JsonPropertyName("post_metadata")]
    public PostMetadata? PostMetadata { get; set; }

    [JsonPropertyName("post_type")]
    public string? PostType { get; set; }

    [JsonPropertyName("preview_asset_type")]
    public string? PreviewAssetType { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("reshare_count")]
    public int ReshareCount { get; set; }

    [JsonPropertyName("teaser_text_json_string")]
    public string? TeaserTextJsonString { get; set; }

    [JsonPropertyName("thumbnail")]
    public PostThumbnail? Thumbnail { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("upgrade_url")]
    public string? UpgradeUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    // Always null in sample; shape unknown — left as raw JSON
    [JsonPropertyName("video_preview")]
    public JsonElement? VideoPreview { get; set; }

    [JsonPropertyName("was_posted_by_campaign_owner")]
    public bool WasPostedByCampaignOwner { get; set; }
}