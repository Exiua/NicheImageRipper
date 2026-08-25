using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.KoreanBj;

public class KoreanBjVideoCapturer : PlaylistCapturer
{
        
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        var url = e.Response.Url;
        return url.Contains("master.m3u8") || url.Contains("master.txt") || url.Contains("ppcheck-prox.php?pp=");
    }

    protected override string GetId(string url)
    {
        if (url.Contains("master.txt"))
        {
            return url.Split("/")[3];
        }

        if (url.Contains("ppcheck-prox.php?pp="))
        {
            return UrlUtility.GetUrlParameterValue(url, "pp");
        }

        return UrlUtility.GetUrlParameterValue(url, "t");
    }
}