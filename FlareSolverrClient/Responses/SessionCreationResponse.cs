using System.Text.Json.Serialization;

namespace FlareSolverrClient.Responses;

public class SessionCreationResponse : BaseResponse
{
    [JsonPropertyName("session")]
    public string Session { get; set; } = null!;
}