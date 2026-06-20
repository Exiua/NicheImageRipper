using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.FileDownloading;

public static class MpdDownloader
{
    public static void DownloadMpd(string mpdUrl, string outputPath)
    {
        var baseUrl = UrlUtility.TrimUrl(mpdUrl);
        // Placeholder for MPD downloading logic
        // This would typically involve using a library to parse the MPD file,
        // download the segments, and then combine them into a single media file.
        throw new NotImplementedException("MPD downloading is not yet implemented.");
    }
}