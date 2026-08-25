namespace Sdk.Exceptions;

public class DownloadTimeoutException : RipperException
{
    public long DownloadedBytesCount { get; init; }
    
    public DownloadTimeoutException(long downloadedBytesCount, string message) : base(message)
    {
        DownloadedBytesCount = downloadedBytesCount;
    }

    public DownloadTimeoutException(long downloadedBytesCount, string message, Exception innerException) : base(message, innerException)
    {
        DownloadedBytesCount = downloadedBytesCount;
    }
}