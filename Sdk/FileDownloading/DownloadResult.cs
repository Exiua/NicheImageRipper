namespace Sdk.FileDownloading;

public readonly record struct DownloadResult(DownloadOutcome Outcome, string? Reason = null)
{
    public static DownloadResult Success() => new(DownloadOutcome.Success);
    public static DownloadResult Retry() => new(DownloadOutcome.Retry);
    public static DownloadResult SkipNotAFailure(string? reason = null) => new(DownloadOutcome.SkipNotAFailure, reason);
    public static DownloadResult Failed(string? reason = null) => new(DownloadOutcome.Failed, reason);
}