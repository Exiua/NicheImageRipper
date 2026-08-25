using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.MissAv;

public class MissAvVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("playlist.m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[3];
    }
}