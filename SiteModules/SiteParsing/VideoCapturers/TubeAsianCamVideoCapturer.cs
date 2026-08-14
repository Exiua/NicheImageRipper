using NicheImageRipper.Core.SiteParsing.VideoCapturers;
using NicheImageRipper.Core.Utility;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.SiteModules.SiteParsing.VideoCapturers;

public class TubeAsianCamVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("master.m3u8");
    }

    protected override string GetId(string url)
    {
        return UrlUtility.GetUrlParameterValue(url, "t");
    }
}