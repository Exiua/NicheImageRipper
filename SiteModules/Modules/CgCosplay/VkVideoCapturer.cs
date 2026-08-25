using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.CgCosplay;

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