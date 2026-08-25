namespace NicheImageRipper.Service.Models;

public class Credentials
{
    public string? Username { get; set; }
    public string? Password { get; set; }

    public static Credentials FromCoreCredentials(Sdk.Configuration.Credentials credentials)
    {
        var creds = new Credentials
        {
            Username = credentials.Username,
            Password = credentials.Password
        };
        return creds;
    }
}