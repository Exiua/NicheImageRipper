using System.Text.Json.Serialization;
using Core.JsonConverters;

namespace Service.Models.Configs;

public class CookieConfig
{
    public string? Twitter { get; set; }
    public string? Newgrounds { get; set; }
    public string? Porn3dx { get; set; }
    public string? Pornhub { get; set; }
    public string? Thothub { get; set; }
    public string? Kemono { get; set; }
    public string? SimpCity { get; set; }
    [JsonConverter(typeof(StringOrArrayConverter))] // required for backward compatibility
    public string[]? Pixiv { get; set; }
    public string? SteamCommunity { get; set; }

    public static CookieConfig FromCoreCookieConfig(Core.Configuration.CookieConfig generalConfigCookies)
    {
        var config = new CookieConfig
        {
            Twitter = generalConfigCookies.Twitter,
            Newgrounds = generalConfigCookies.Newgrounds,
            Porn3dx = generalConfigCookies.Porn3dx,
            Pornhub = generalConfigCookies.Pornhub,
            Thothub = generalConfigCookies.Thothub,
            Kemono = generalConfigCookies.Kemono,
            SimpCity = generalConfigCookies.SimpCity,
            Pixiv = generalConfigCookies.Pixiv,
            SteamCommunity = generalConfigCookies.SteamCommunity
        };
        return config;
    }
}