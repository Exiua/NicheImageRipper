using NicheImageRipper.Core.Utility;
using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.Core.SiteParsing.VideoCapturers;

public class PussySpaceCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("master.m3u8");
    }

    protected override string GetId(string url)
    {
        return UrlUtility.GetUrlParameterValue(url, "_tid");
    }
}