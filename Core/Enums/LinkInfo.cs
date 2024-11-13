namespace Core.Enums;

public enum LinkInfo
{
    None,           // Normal
    M3U8,           // Requires ffmpeg to download
    GDrive,         // Requires using the Google Drive API to download
    IframeMedia,    // Requires BunnyVideoDrm to download
    Mega,           // Requires MegaCmd to download
    PixelDrain,     // Requires PixelDrain API to download
    Youtube,        // Requires yt-dlp to download
    Text,           // Save as text file
    GoFile,         // Requires cookies and special re-check logic
    MpegDash,       // Requires yt-dlp to download
    ResolveImage,   // Requires the image link to be resolved to the download link in the ImageRipper
}