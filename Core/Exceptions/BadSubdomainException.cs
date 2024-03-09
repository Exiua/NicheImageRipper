namespace Core.Exceptions;

public class BadSubdomainException : Exception
{
    public BadSubdomainException()
    {
    }

    public BadSubdomainException(string message) : base(message)
    {
    }

    public BadSubdomainException(string message, Exception inner) : base(message, inner)
    {
    }
}