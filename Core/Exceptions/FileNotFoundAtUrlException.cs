namespace Core.Exceptions;

public class FileNotFoundAtUrlException : RipperException
{
    public FileNotFoundAtUrlException()
    {
    }

    public FileNotFoundAtUrlException(string message) : base(message)
    {
    }

    public FileNotFoundAtUrlException(string message, Exception inner) : base(message, inner)
    {
    }
}