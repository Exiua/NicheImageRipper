using System.Text.Json.Serialization;

namespace NicheImageRipper.Core.Clients.Iwara.Models;

public class LoginRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; set; }
    [JsonPropertyName("password")]
    public required string Password { get; set; }
}