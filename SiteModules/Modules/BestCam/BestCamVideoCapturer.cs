using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.BestCam;

public class BestCamVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        var responseUrl = e.Response.Url;
        return responseUrl.Contains("bestcam.tv") && responseUrl.Contains(".m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[4].Split("?")[0];
    }
}