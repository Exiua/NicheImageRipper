namespace Core.Exceptions;

public class FileNotFoundAtUrlException : Exception
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