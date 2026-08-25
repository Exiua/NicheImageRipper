using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

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