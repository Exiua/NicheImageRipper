using NicheImageRipper.Core.Utility;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.Core.SiteParsing.VideoCapturers;

public class XHamsterCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        var url = e.Response.Url;
        return url.Contains("_TPL_.h264.mp4.m3u8");
    }

    protected override string GetId(string url)
    {
        return UrlUtility.GetUrlParameterValue(url, "key");
    }
}