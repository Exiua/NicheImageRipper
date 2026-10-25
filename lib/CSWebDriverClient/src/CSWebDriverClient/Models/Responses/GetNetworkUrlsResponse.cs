using System.Text.Json.Serialization;

namespace CSWebDriverClient.Models.Responses;

/// <summary>
///     Response model containing network URLs retrieved during page load.
/// </summary>
public class GetNetworkUrlsResponse : BaseResponse
{
    /// <summary>
    ///     List of network URLs retrieved during the page load.
    /// </summary>
    [JsonPropertyName("urls")]
    public required List<string> Urls { get; set; }
}