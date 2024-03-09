namespace Core.Exceptions;

public class ImproperlyFormattedSubdomainException : Exception
{
    public ImproperlyFormattedSubdomainException()
    {
    }

    public ImproperlyFormattedSubdomainException(string message) : base(message)
    {
    }

    public ImproperlyFormattedSubdomainException(string message, Exception inner) : base(message, inner)
    {
    }
}