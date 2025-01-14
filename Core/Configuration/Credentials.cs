namespace Core.Configuration;

public class Credentials
{
    public required string Username { get; set; }
    public required string Password { get; set; }

    public void Deconstruct(out string username, out string password)
    {
        username = Username;
        password = Password;
    }
}