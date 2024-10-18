using System.Text.Json.Serialization;

namespace FlareSolverrClient.Responses;

public class SessionListResponse : BaseResponse
{
    [JsonPropertyName("sessions")]
    public List<string> Sessions { get; set; } = null!;
}