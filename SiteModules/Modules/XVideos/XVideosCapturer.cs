using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.SiteModules.Modules.XVideos;

public class XVideosCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("hls.m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[^2];
    }
}