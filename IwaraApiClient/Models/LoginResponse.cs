using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class LoginResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }
}