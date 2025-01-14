namespace Core.Exceptions;

public class BadSubdomainException : RipperException
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