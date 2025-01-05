namespace Core.Exceptions;

public class FailedToDeleteSessionException : RipperException
{
    public FailedToDeleteSessionException() : base("Failed to delete session")
    {
    }

    public FailedToDeleteSessionException(Exception inner) : base("Failed to delete session", inner)
    {
    }
}