using System.Text;
using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.FileDownloading;

public static class DownloadLogging
{
    public static void LogFailedUrl(string url)
    {
        using var writer = new StreamWriter(AppData.GetPath("failed.txt"), true, Encoding.Unicode);
        writer.WriteLine(url);
    }
}