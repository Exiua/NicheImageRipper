using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class ErrorMessage
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }
}