namespace NicheImageRipper.Service.Models.Configs;

public class KeyConfig
{
    public string? Imgur { get; set; }
    public string? Google { get; set; }
    public string? Dropbox { get; set; }
    public string? Pixeldrain { get; set; }
    public string? Pixiv { get; set; } // Refresh token for Pixiv API

    public static KeyConfig FromCoreKeyConfig(NicheImageRipper.Core.Configuration.KeyConfig generalConfigKeys)
    {
        var config = new KeyConfig
        {
            Imgur = generalConfigKeys.Imgur,
            Google = generalConfigKeys.Google,
            Dropbox = generalConfigKeys.Dropbox,
            Pixeldrain = generalConfigKeys.Pixeldrain,
            Pixiv = generalConfigKeys.Pixiv
        };
        return config;
    }
}