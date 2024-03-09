namespace Core;

public class Token(string value, DateTime expiration)
{
    public string Value { get; set; } = value;
    public DateTime Expiration { get; set; } = expiration;

    public override string ToString()
    {
        return Value;
    }
}