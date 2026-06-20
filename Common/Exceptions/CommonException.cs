namespace NicheImageRipper.Common.Exceptions;

public class CommonException : Exception
{
    public CommonException()
    {
    }

    public CommonException(string message) : base(message)
    {
    }

    public CommonException(string message, Exception inner) : base(message, inner)
    {
    }
}