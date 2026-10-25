using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CSWebDriverClient.Models.Requests;

/// <summary>
///     Request model for retrieving network URLs.
/// </summary>
public class GetNetworkUrlsRequest
{
    /// <summary>
    ///     The URL to retrieve.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }

    /// <summary>
    ///     Timeout in seconds for the request. (default: 5 seconds)
    /// </summary>
    [Range(0, double.MaxValue)]
    [JsonPropertyName("timeout")]
    public double Timeout { get; set; }

    public GetNetworkUrlsRequest(string url, double timeoutSeconds = 5.0)
    {
        Url = url;
        Timeout = timeoutSeconds;
    }
}