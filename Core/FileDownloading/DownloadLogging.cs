using System.Text;

namespace NicheImageRipper.Core.FileDownloading;

public static class DownloadLogging
{
    public static void LogFailedUrl(string url)
    {
        using var writer = new StreamWriter("failed.txt", true, Encoding.Unicode);
        writer.WriteLine(url);
    }
}