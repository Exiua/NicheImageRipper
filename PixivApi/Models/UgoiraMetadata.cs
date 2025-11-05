using System.Text.Json.Serialization;

namespace PixivApi.Models;

public class UgoiraMetadata : PixivModel
{
    [JsonPropertyName("error")]
    public bool Error { get; set; }
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("body")]
    public UgoiraMetadataBody? Body { get; set; }
    
    public class UgoiraMetadataBody
    {
        [JsonPropertyName("src")]
        public string Src { get; set; } = string.Empty;
        [JsonPropertyName("originalSrc")]
        public string OriginalSrc { get; set; } = string.Empty;
        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = string.Empty;
        [JsonPropertyName("frames")]
        public List<UgoiraFrame> Frames { get; set; } = [];
    }
    
    public class UgoiraFrame
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = string.Empty;
        [JsonPropertyName("delay")]
        public int Delay { get; set; }
    }
}