namespace Core.Exceptions;

public class FailedToCreateSession : RipperException
{
    public FailedToCreateSession() : base("Failed to create session")
    {
    }

    public FailedToCreateSession(Exception inner) : base("Failed to create session", inner)
    {
    }
}