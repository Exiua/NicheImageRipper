using NicheImageRipper.Core.ExtensionMethods;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.Core.SiteParsing.VideoCapturers;

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