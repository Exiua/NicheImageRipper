namespace NicheImageRipper.Core.Configuration;

public class Credentials
{
    public required string Username { get; set; }
    public required string Password { get; set; }

    public void Deconstruct(out string username, out string password)
    {
        username = Username;
        password = Password;
    }
    
    public static Credentials New()
    {
        return new Credentials
        {
            Username = "",
            Password = ""
        };
    }
}

public static class CredentialsExtensions
{
    public static (string? username, string? password) Deconstruct(this Credentials? credentials)
    {
        return credentials is null ? (null, null) : (credentials.Username, credentials.Password);
    }
}