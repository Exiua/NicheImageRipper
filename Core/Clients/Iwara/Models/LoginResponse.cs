using System.Text.Json.Serialization;

namespace NicheImageRipper.Core.Clients.Iwara.Models;

public class LoginResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }
}