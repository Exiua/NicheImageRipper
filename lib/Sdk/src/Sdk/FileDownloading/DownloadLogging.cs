namespace NicheImageRipper.Sdk.FileDownloading;

public static class DownloadLogging
{
    [Obsolete("Download failures are recorded by the ripper. For other failures use FailedUrlLog.Record, " +
              "which also records the source and reason.")]
    public static void LogFailedUrl(string url) =>
        FailedUrlLog.Record(FailedUrlEntry.Create(FailureStage.Download, FailureKind.Other, "Unknown", url));
}
