using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Responses;

public class SessionListResponse : BaseResponse
{
    [JsonPropertyName("sessions")]
    public List<string> Sessions { get; set; } = null!;
}