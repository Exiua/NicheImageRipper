namespace MegaSdk;

public sealed class MegaClientOptions
{
    /// <summary>
    ///     MEGA app key. Not needed: the SDK (v10.22.0, see native/sdk) ignores it, and MEGA no longer issues them.
    /// </summary>
    public string? AppKey { get; init; }

    /// <summary>
    ///     Directory for the SDK's local cache. The current working directory is used if null.
    /// </summary>
    public string? BasePath { get; init; }

    /// <summary>
    ///     User agent for network requests. The SDK's default is used if null.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    ///     Worker threads for encryption and other operations.
    /// </summary>
    public uint WorkerThreadCount { get; init; } = 1;
}
