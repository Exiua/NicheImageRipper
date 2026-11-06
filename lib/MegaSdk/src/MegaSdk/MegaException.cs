namespace MegaSdk;

public class MegaException : Exception
{
    public MegaErrorCode ErrorCode { get; }

    public MegaException(MegaErrorCode errorCode, string? message)
        : base(message ?? MegaClient.GetErrorString(errorCode))
    {
        ErrorCode = errorCode;
    }
}
