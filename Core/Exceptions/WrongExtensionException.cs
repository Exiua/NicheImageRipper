namespace Core.Exceptions;

public class WrongExtensionException : Exception
{
    public WrongExtensionException()
    {
    }

    public WrongExtensionException(string message) : base(message)
    {
    }

    public WrongExtensionException(string message, Exception inner) : base(message, inner)
    {
    }
}