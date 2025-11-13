using System.Text.Json.Serialization;

namespace CSWebDriverClient.Models.Responses;

/// <summary>
/// Represents an error response returned when something goes wrong.
/// </summary>
public class ErrorResponse : BaseResponse
{
    /// <summary>
    /// Error message describing what went wrong.
    /// </summary>
    [JsonPropertyName("error")]
    public required string Error { get; set; }

    /// <summary>
    ///     Additional details about the error.
    /// </summary>
    [JsonPropertyName("details")]
    public string? Details { get; set; }
}
