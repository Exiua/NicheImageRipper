namespace NicheImageRipper.Core.FileDownloading;

public enum DownloadOutcome
{
    Success,        // file is good, done
    Retry,          // try the same download again (optionally with adjusted state, e.g. link.Url changed)
    SkipNotAFailure,// stop trying, don't count as failed (pixiv's "doesn't exist" case)
    Failed,         // stop trying, count as failed
}