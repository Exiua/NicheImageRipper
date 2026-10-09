using System.Text.Json.Serialization;

namespace PatreonApiClient.Models;

public sealed class PatreonMediaAttributes
{
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("display")]
    public MediaDisplay? Display { get; set; }
    
    [JsonPropertyName("state")]
    public string? State { get; set; }
}