using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class AuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;
    [JsonPropertyName("user")]
    public AuthResponseUser User { get; set; } = null!;
    
    public class AuthResponseUser
    {
        [JsonPropertyName("account")]
        public string Account { get; set; } = null!;
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;
        [JsonPropertyName("is_mail_authorized")]
        public bool IsMailAuthorized { get; set; }
        [JsonPropertyName("is_premium")]
        public bool IsPremium { get; set; }
        [JsonPropertyName("mail_address")]
        public string MailAddress { get; set; } = null!;
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;
        [JsonPropertyName("profile_image_urls")]
        public Dictionary<string, string> ProfileImageUrls { get; set; } = null!;
        [JsonPropertyName("require_policy_agreement")]
        public bool RequirePolicyAgreement { get; set; }
        [JsonPropertyName("x_restrict")]
        public int XRestrict { get; set; }
    }
}
