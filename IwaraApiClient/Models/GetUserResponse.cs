using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class GetUserResponse
{
    public required string Body { get; set; }
    public required MediaMetadata Header { get; set; }
    public required User User { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class User
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("username")]
    public required string Username { get; set; }
    [JsonPropertyName("status")]
    public required string Status { get; set; }
    [JsonPropertyName("role")]
    public required string Role { get; set; }
    [JsonPropertyName("followedBy")]
    public required bool FollowedBy { get; set; }
    [JsonPropertyName("following")]
    public required bool Following { get; set; }
    [JsonPropertyName("friend")]
    public required bool Friend { get; set; }
    [JsonPropertyName("premium")]
    public required bool Premium { get; set; }
    [JsonPropertyName("creatorProgram")]
    public required bool CreatorProgram { get; set; }
    [JsonPropertyName("locale")]
    public required string? Locale { get; set; }
    [JsonPropertyName("seenAt")]
    public required DateTime SeenAt { get; set; }
    [JsonPropertyName("avatar")]
    public required MediaMetadata Avatar { get; set; }
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; set; }
}