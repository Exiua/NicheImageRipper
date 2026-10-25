using System.Text.Json.Serialization;

namespace NHentaiApi.Models;

public class GalleryDownloadUrl
{
    private DateTimeOffset? _expiresAtDate;
    
    public string? Url { get; set; }

    public long ExpiresAt { get; set; }

    [JsonIgnore]
    public DateTimeOffset ExpiresAtDate
    {
        get
        {
            return _expiresAtDate ??= DateTimeOffset.FromUnixTimeSeconds(ExpiresAt);
        }
    }
}