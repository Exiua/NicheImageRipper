namespace NicheImageRipper.Sdk.Features;

/// <summary>
/// Tracks whether named external tools/features (ffmpeg, yt-dlp, FlareSolverr, etc.) are available,
/// detecting and caching each on first check. Keys are free-form strings so any
/// parser or download strategy can query its own external dependency without Sdk needing to know it exists.
/// </summary>
public interface IAvailableFeatures
{
    /// <summary>
    /// Returns whether <paramref name="key"/> is available, running <paramref name="detect"/> and
    /// caching the result if this is the first check for that key.
    /// </summary>
    bool HasFeature(string key, Func<bool> detect);

    /// <summary>
    /// Returns the cached availability for <paramref name="key"/>, or false if it has never been checked.
    /// </summary>
    bool HasFeature(string key);
}