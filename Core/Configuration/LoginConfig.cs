using System.Text.Json.Serialization;

namespace Core.Configuration;

public class LoginConfig
{
    public required Credentials DeviantArt { get; set; }
    public required Credentials Mega { get; set; }
    public required Credentials TitsInTops { get; set; }
    public required Credentials Nijie { get; set; }
    public required Credentials Danbooru { get; set; }
    public required Credentials Gelbooru { get; set; }
    public required Credentials Rule34 { get; set; }
    public required Credentials Yandere { get; set; }
    public required Credentials E621 { get; set; }
    [JsonPropertyName("E-Hentai")]
    public required Credentials EHentai { get; set; }
    public required Credentials SteamCommunity { get; set; }

    public static LoginConfig New()
    {
        return new LoginConfig
        {
            DeviantArt = Credentials.New(),
            Mega = Credentials.New(),
            TitsInTops = Credentials.New(),
            Nijie = Credentials.New(),
            Danbooru = Credentials.New(),
            Gelbooru = Credentials.New(),
            Rule34 = Credentials.New(),
            Yandere = Credentials.New(),
            E621 = Credentials.New(),
            EHentai = Credentials.New(),
            SteamCommunity = Credentials.New(),
        };
    }
}