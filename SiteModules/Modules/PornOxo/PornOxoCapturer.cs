using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.SiteModules.Modules.PornOxo;

public class PornOxoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        var url = e.Response.Url;
        return url.Contains("index.m3u8") || (url.Contains(".mp4") && url.Contains("cdn.pornoxo.com"));
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[10].Split(".")[0];
    }
}