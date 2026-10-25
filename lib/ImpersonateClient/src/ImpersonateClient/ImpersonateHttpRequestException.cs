namespace ImpersonateClient;

public sealed class ImpersonateHttpRequestException : Exception
{
    public ImpersonateHttpRequestException(string message)
        : base(message)
    {
    }

    public ImpersonateHttpRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}