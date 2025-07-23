namespace Core.Configuration;

public class KeyConfig
{
    public required string Imgur { get; set; }
    public required string Google { get; set; }
    public required string Dropbox { get; set; }
    public required string Pixeldrain { get; set; }
    
    public static KeyConfig New()
    {
        return new KeyConfig
        {
            Imgur = "",
            Google = "",
            Dropbox = "",
            Pixeldrain = ""
        };
    }
}