namespace Core.Exceptions;

public class FailedToGetSolutionException : RipperException
{
    public FailedToGetSolutionException() : base("Failed to get site solution")
    {
    }

    public FailedToGetSolutionException(Exception inner) : base("Failed to get site solution", inner)
    {
    }
}