namespace Core.Exceptions;

public class RipperException : Exception
{
    public RipperException()
    {
    }

    public RipperException(string message) : base(message)
    {
    }

    public RipperException(string message, Exception inner) : base(message, inner)
    {
    }
}