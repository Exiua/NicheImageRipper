using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.SiteParsing;
using OpenQA.Selenium.BiDi.Network;

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