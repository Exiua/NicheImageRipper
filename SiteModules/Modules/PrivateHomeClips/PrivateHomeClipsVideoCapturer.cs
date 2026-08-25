using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.PrivateHomeClips;

public class PrivateHomeClipsVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains(".m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/").Skip(8).Take(2).JoinWith("/");
    }
}