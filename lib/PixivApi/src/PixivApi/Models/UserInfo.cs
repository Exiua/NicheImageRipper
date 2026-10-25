using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class UserInfo : PixivModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    [JsonPropertyName("account")]
    public string Account { get; set; } = null!;
    [JsonPropertyName("profile_image_urls")]
    public Dictionary<string, string> ProfileImageUrls { get; set; } = null!;
    [JsonPropertyName("comment")]
    public string? Comment { get; set; } // present only on `user_detail` endpoint
    [JsonPropertyName("is_followed")]
    public bool? IsFollowed { get; set; }
    [JsonPropertyName("is_access_blocking_user")]
    public bool? IsAccessBlockingUser { get; set; }
    [JsonPropertyName("is_accepting_request")]
    public bool? IsAcceptingRequest { get; set; } // present only on `user_following` and `user_follower` endpoint
}