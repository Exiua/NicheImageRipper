using System.Text.Json.Serialization;

namespace IwaraApiClient.Models;

public class VideoDownloadMetadata
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("src")]
    public required Metadata Src { get; set; }
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt {get; set;}
    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt {get; set;}
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    public class Metadata
    {
        [JsonPropertyName("view")]
        public required string View {get; set;}
        [JsonPropertyName("download")]
        public required string Download  {get; set;}
    }
}