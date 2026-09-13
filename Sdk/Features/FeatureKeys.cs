namespace NicheImageRipper.Sdk.Features;

/// <summary>
/// String keys for the external tools Sdk's own infrastructure checks (e.g. <c>ProcessRunner.RunFfmpeg</c>).
/// Not exhaustive - any parser or download strategy is free to call <see cref="IAvailableFeatures.HasFeature(string,System.Func{bool})"/>
/// with its own key for a tool Sdk has no built-in awareness of.
/// </summary>
public static class FeatureKeys
{
    public const string Ffmpeg = "ffmpeg";
    public const string YtDlp = "yt-dlp";
    public const string MegaCmd = "mega-cmd";
    public const string FlareSolverr = "flaresolverr";
    public const string CSWebDriver = "cs-webdriver";
    public const string SteamCmd = "steamcmd";
}