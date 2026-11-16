using SteamKit2;

namespace SteamApiClient;

public sealed class SteamApiException : Exception
{
    public SteamApiException(string message, EResult? result = null) : base(message)
    {
        Result = result;
    }

    public SteamApiException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>The Steam result code that caused the failure, if there was one.</summary>
    public EResult? Result { get; }
}
