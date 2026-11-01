using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.FileDownloading;

/// <summary>One line of <see cref="FailedUrlLog"/>.</summary>
/// <param name="Url">The URL that couldn't be handled: the gallery for parse and rip failures, the file for downloads.</param>
/// <param name="GalleryUrl">The gallery being ripped when <paramref name="Url"/> is something inside it, otherwise null.</param>
/// <param name="Source">Type name of the parser or download strategy that failed.</param>
public sealed record FailedUrlEntry(
    DateTimeOffset Timestamp,
    FailureStage Stage,
    FailureKind Kind,
    string Source,
    string Url,
    string? GalleryUrl,
    string? Reason)
{
    // Base64 links are whole files inlined into the URL
    private const int MaxUrlLength = 500;

    public static FailedUrlEntry Create(FailureStage stage, FailureKind kind, string source, string url,
                                        string? galleryUrl = null, string? reason = null) =>
        new(DateTimeOffset.Now, stage, kind, source, Shorten(url), galleryUrl is null ? null : Shorten(galleryUrl),
            reason);

    private static string Shorten(string url) => url.Length > MaxUrlLength ? UrlUtility.TruncateLongUrl(url) : url;
}
