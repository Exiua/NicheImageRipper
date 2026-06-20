using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.Core.SiteParsing.VideoCapturers;

public class VkVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.MimeType == "application/dash+xml";
    }

    protected override string GetId(string url)
    {
        return url.Split("id=")[^1].Split("&")[0];
    }
}