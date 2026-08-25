using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Twitter;

public class TwitterVideoCapturer : PlaylistCapturer
{
    protected override string GetId(string url)
    {
        return url.Split("/")[4];
    }
    
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains(".m3u8");
    }
}