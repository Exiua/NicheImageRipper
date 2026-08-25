using OpenQA.Selenium.BiDi.Network;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.PmvHaven;

public class PmvHavenCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("master.m3u8");
    }

    protected override string GetId(string url)
    {
        var slashCount = url.Count(c => c == '/');
        return slashCount == 4 ? url.Split('/')[3] : url.Split('/')[4];
    }
}