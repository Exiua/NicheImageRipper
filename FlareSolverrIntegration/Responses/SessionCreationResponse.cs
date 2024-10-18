using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Responses;

public class SessionCreationResponse : BaseResponse
{
    [JsonPropertyName("session")]
    public string Session { get; set; } = null!;
}