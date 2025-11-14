using System.Text.Json.Serialization;

namespace CSWebDriverClient.Models.Responses;

public class PageResponse : BaseResponse
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}