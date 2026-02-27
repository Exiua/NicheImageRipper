using System.Text.Json.Serialization;
using Core.JsonConverters;

namespace Core.Configuration;

public class CookieConfig
{
    public required string Twitter { get; set; }
    public required string Newgrounds { get; set; }
    public required string Porn3dx { get; set; }
    public required string Pornhub { get; set; }
    public required string Thothub { get; set; }
    public required string Kemono { get; set; }
    public required string SimpCity { get; set; }
    [JsonConverter(typeof(StringOrArrayConverter))] // required for backward compatibility
    public required string[] Pixiv { get; set; }
    public required string SteamCommunity { get; set; }
    
    public static CookieConfig New()
    {
        return new CookieConfig
        {
            Twitter = "",
            Newgrounds = "",
            Porn3dx = "",
            Pornhub = "",
            Thothub = "",
            Kemono = "",
            SimpCity = "",
            Pixiv = [],
            SteamCommunity = "",
        };
    }
}
