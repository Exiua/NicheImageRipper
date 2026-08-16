namespace NicheImageRipper.Core.Enums;

/// <summary>
///     Identifies which download mechanism a FileLink requires. Core defines the generic, non-site-specific
///     mechanisms as constants below; site modules define their own string constants for site-specific
///     mechanisms (e.g. "mega", "gdrive") next to whichever IFileDownloadStrategy handles them.
/// </summary>
public readonly record struct LinkInfo(string Name)
{
    public const string NoneValue = "none";
    public const string TextValue = "text";
    public const string Base64Value = "base64";
    public const string ResolveImageValue = "resolve-image";
    public const string SeleniumImageValue = "selenium-image";
    public const string M3U8FfmpegValue = "m3u8-ffmpeg";
    public const string M3U8YtDlpValue = "m3u8-ytdlp";
    public const string ObfuscatedM3U8Value = "obfuscated-m3u8";
    public const string MpegDashValue = "mpeg-dash";
    public const string IframeMediaValue = "iframe-media";

    public static readonly LinkInfo None = new(NoneValue);
    public static readonly LinkInfo Text = new(TextValue);
    public static readonly LinkInfo Base64 = new(Base64Value);
    public static readonly LinkInfo ResolveImage = new(ResolveImageValue);
    public static readonly LinkInfo SeleniumImage = new(SeleniumImageValue);
    public static readonly LinkInfo M3U8Ffmpeg = new(M3U8FfmpegValue);
    public static readonly LinkInfo M3U8YtDlp = new(M3U8YtDlpValue);
    public static readonly LinkInfo ObfuscatedM3U8 = new(ObfuscatedM3U8Value);
    public static readonly LinkInfo MpegDash = new(MpegDashValue);
    public static readonly LinkInfo IframeMedia = new(IframeMediaValue);

    public bool Equals(LinkInfo other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    public override string ToString() => Name;
}