namespace NicheImageRipper.Sdk.FileDownloading;

public readonly record struct DownloadResult(DownloadOutcome Outcome, string? Reason = null)
{
    /// <summary>
    ///     Why the download failed, for <see cref="FailedUrlLog"/>. A <see cref="DownloadOutcome.Failed"/> result is
    ///     recorded as <see cref="FileDownloading.FailureKind.Other"/> when this is null; a
    ///     <see cref="DownloadOutcome.SkipNotAFailure"/> result is only recorded when this is set.
    /// </summary>
    public FailureKind? FailureKind { get; init; }

    public static DownloadResult Success() => new(DownloadOutcome.Success);
    public static DownloadResult Retry() => new(DownloadOutcome.Retry);
    public static DownloadResult SkipNotAFailure(string? reason = null) => new(DownloadOutcome.SkipNotAFailure, reason);

    public static DownloadResult SkipNotAFailure(string? reason, FailureKind failureKind) =>
        new(DownloadOutcome.SkipNotAFailure, reason) { FailureKind = failureKind };

    public static DownloadResult Failed(string? reason = null) => new(DownloadOutcome.Failed, reason);

    public static DownloadResult Failed(string? reason, FailureKind failureKind) =>
        new(DownloadOutcome.Failed, reason) { FailureKind = failureKind };

    /// <summary>A failure because the file is gone or missing.</summary>
    public static DownloadResult NotFound(string? reason = null) =>
        Failed(reason, FileDownloading.FailureKind.NotFound);
}
