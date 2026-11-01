namespace NicheImageRipper.Sdk.FileDownloading;

/// <summary>Which part of a rip failed, as recorded in <see cref="FailedUrlLog"/>.</summary>
public enum FailureStage
{
    /// <summary>A parser couldn't turn a gallery (or an item inside it) into file links.</summary>
    Parse,

    /// <summary>A download strategy couldn't fetch a file link.</summary>
    Download,

    /// <summary>The gallery as a whole failed after its retries, for a reason not recorded as a parse failure.</summary>
    Rip,
}
