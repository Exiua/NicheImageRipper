using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PostRelationships
{
    [JsonPropertyName("access_rules")]
    public ToManyRelationship? AccessRules { get; set; }

    [JsonPropertyName("attachments_media")]
    public ToManyRelationship? AttachmentsMedia { get; set; }

    [JsonPropertyName("audio")]
    public ToOneRelationship? Audio { get; set; }

    [JsonPropertyName("audio_preview")]
    public ToOneRelationship? AudioPreview { get; set; }

    [JsonPropertyName("authors")]
    public ToManyRelationship? Authors { get; set; }

    [JsonPropertyName("campaign")]
    public ToOneRelationship? Campaign { get; set; }

    [JsonPropertyName("content_unlock_options")]
    public ToManyRelationship? ContentUnlockOptions { get; set; }

    [JsonPropertyName("custom_thumbnail_media")]
    public ToOneRelationship? CustomThumbnailMedia { get; set; }

    [JsonPropertyName("drop")]
    public ToOneRelationship? Drop { get; set; }

    [JsonPropertyName("images")]
    public ToManyRelationship? Images { get; set; }

    [JsonPropertyName("livestream")]
    public ToOneRelationship? Livestream { get; set; }

    [JsonPropertyName("media")]
    public ToManyRelationship? Media { get; set; }

    [JsonPropertyName("poll")]
    public ToOneRelationship? Poll { get; set; }

    [JsonPropertyName("post_new_comment_identity")]
    public ToOneRelationship? PostNewCommentIdentity { get; set; }

    [JsonPropertyName("primary_image")]
    public ToOneRelationship? PrimaryImage { get; set; }

    [JsonPropertyName("rss_synced_feed")]
    public ToOneRelationship? RssSyncedFeed { get; set; }

    [JsonPropertyName("shows")]
    public ToManyRelationship? Shows { get; set; }

    [JsonPropertyName("user")]
    public ToOneRelationship? User { get; set; }

    [JsonPropertyName("user_defined_tags")]
    public ToManyRelationship? UserDefinedTags { get; set; }

    [JsonPropertyName("video")]
    public ToOneRelationship? Video { get; set; }
}