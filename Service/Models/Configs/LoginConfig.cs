using System.Text.Json.Serialization;

namespace Service.Models.Configs;

public class LoginConfig
{
    public Credentials? DeviantArt { get; set; }
    public Credentials? Mega { get; set; }
    public Credentials? TitsInTops { get; set; }
    public Credentials? Nijie { get; set; }
    public Credentials? Danbooru { get; set; }
    public Credentials? Gelbooru { get; set; }
    public Credentials? Rule34 { get; set; }
    public Credentials? Yandere { get; set; }
    public Credentials? E621 { get; set; }
    [JsonPropertyName("E-Hentai")]
    public Credentials? EHentai { get; set; }
    public Credentials? SteamCommunity { get; set; }

    public static LoginConfig FromCoreLoginConfig(Core.Configuration.LoginConfig generalConfigLogins)
    {
        var config = new LoginConfig
        {
            DeviantArt = Credentials.FromCoreCredentials(generalConfigLogins.DeviantArt),
            Mega = Credentials.FromCoreCredentials(generalConfigLogins.Mega),
            TitsInTops = Credentials.FromCoreCredentials(generalConfigLogins.TitsInTops),
            Nijie = Credentials.FromCoreCredentials(generalConfigLogins.Nijie),
            Danbooru = Credentials.FromCoreCredentials(generalConfigLogins.Danbooru),
            Gelbooru = Credentials.FromCoreCredentials(generalConfigLogins.Gelbooru),
            Rule34 = Credentials.FromCoreCredentials(generalConfigLogins.Rule34),
            Yandere = Credentials.FromCoreCredentials(generalConfigLogins.Yandere),
            E621 = Credentials.FromCoreCredentials(generalConfigLogins.E621),
            EHentai = Credentials.FromCoreCredentials(generalConfigLogins.EHentai),
            SteamCommunity = Credentials.FromCoreCredentials(generalConfigLogins.SteamCommunity),
        };
        return config;
    }
}