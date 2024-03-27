namespace Core.Configuration;

public class Credentials
{
    public string Username { get; set; }
    public string Password { get; set; }

    public void Deconstruct(out string username, out string password)
    {
        username = Username;
        password = Password;
    }
}