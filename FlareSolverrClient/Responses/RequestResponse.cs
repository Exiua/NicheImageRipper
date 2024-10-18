using System.Text.Json.Serialization;

namespace FlareSolverrClient.Responses;

public class RequestResponse : BaseResponse
{
    [JsonPropertyName("solution")]
    public Solution Solution { get; set; } = null!;
}
