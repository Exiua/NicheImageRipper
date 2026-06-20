namespace NicheImageRipper.Core.Enums;

public enum LinkInfo
{
    None,           // Normal
    M3U8Ffmpeg,     // Requires ffmpeg to download m3u8 files
    GDrive,         // Requires using the Google Drive API to download
    IframeMedia,    // Requires BunnyVideoDrm to download
    Mega,           // Requires MegaCmd to download
    PixelDrain,     // Requires PixelDrain API to download
    YoutubeVideo,   // Requires yt-dlp to download video
    Text,           // Save as text file
    GoFile,         // Requires cookies and special re-check logic
    MpegDash,       // Requires yt-dlp to download
    ResolveImage,   // Requires the image link to be resolved to the download link in the ImageRipper
    M3U8YtDlp,      // Requires yt-dlp to download m3u8 files
    SeleniumImage,  // Requires Selenium to download the image
    Base64,         // Base64 encoded image
    ObfuscatedM3U8, // Requires deobfuscation of each segment before concatenation with ffmpeg
    PixivUgoira,    // Requires downloading the Ugoira zip and converting to gif or mp4
    SteamCommunity, // Requires SteamCMD to download, and special handling to move the file from the SteamCMD directory to the final destination
}