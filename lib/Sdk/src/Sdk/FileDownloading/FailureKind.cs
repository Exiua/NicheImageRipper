namespace NicheImageRipper.Sdk.FileDownloading;

/// <summary>Why a URL couldn't be parsed or downloaded, as recorded in <see cref="FailedUrlLog"/>.</summary>
public enum FailureKind
{
    /// <summary>Anything not covered below: network errors, rate limits, timeouts, unexpected exceptions.</summary>
    Other,

    /// <summary>The resource is gone or missing (404/410, deleted post, file not found).</summary>
    NotFound,

    /// <summary>
    ///     A page or link shape the parser or downloader doesn't handle, usually a new case on the site (unsupported
    ///     page type, expected element missing).
    /// </summary>
    Unsupported,
}
