using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class AttachmentPreviewMetadata
{
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("mimetype")]
    public string? Mimetype { get; set; }

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }
}