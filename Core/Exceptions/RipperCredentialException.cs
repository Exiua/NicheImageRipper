namespace Core.Exceptions;

public class RipperCredentialException : RipperException
{
    public RipperCredentialException()
    {
    }
    
    public RipperCredentialException(string message) : base(message)
    {
    }
    
    public RipperCredentialException(string message, Exception innerException) : base(message, innerException)
    {
    }
}