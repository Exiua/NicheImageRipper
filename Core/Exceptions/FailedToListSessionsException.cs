namespace Core.Exceptions;

public class FailedToListSessionsException : RipperException
{
    public FailedToListSessionsException() : base("Failed to list sessions")
    {
    }

    public FailedToListSessionsException(Exception inner) : base("Failed to list sessions", inner)
    {
    }
}