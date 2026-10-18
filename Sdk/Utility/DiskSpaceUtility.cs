namespace NicheImageRipper.Sdk.Utility;

public static class DiskSpaceUtility
{
    // Windows reports a full disk as ERROR_DISK_FULL or ERROR_HANDLE_DISK_FULL (as HRESULTs); on Linux and macOS
    // .NET surfaces the raw errno, ENOSPC
    private const int WindowsDiskFull = unchecked((int)0x80070070);
    private const int WindowsHandleDiskFull = unchecked((int)0x80070027);
    private const int UnixNoSpace = 28;

    /// <summary>
    ///     True if <paramref name="exception"/>, or anything it wraps, is an I/O error caused by the disk being full.
    ///     Once that happens every later write fails the same way, so callers should stop rather than carry on.
    /// </summary>
    public static bool IsDiskFull(Exception? exception)
    {
        return exception switch
        {
            null => false,
            IOException io when IsDiskFullCode(io.HResult) => true,
            AggregateException aggregate => aggregate.InnerExceptions.Any(IsDiskFull),
            _ => IsDiskFull(exception.InnerException),
        };
    }

    private static bool IsDiskFullCode(int hResult) => OperatingSystem.IsWindows()
        ? hResult is WindowsDiskFull or WindowsHandleDiskFull
        : hResult == UnixNoSpace;
}
