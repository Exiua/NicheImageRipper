using System.Text.Json.Serialization;

namespace FlareSolverrIntegration.Responses;

public class RequestResponse : BaseResponse
{
    [JsonPropertyName("solution")]
    public Solution Solution { get; set; } = null!;
}
