using NicheImageRipper.Core.ExtensionMethods;
using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.SiteModules.SiteParsing.VideoCapturers;

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