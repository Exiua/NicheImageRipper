using OpenQA.Selenium.BiDi.Network;

namespace NicheImageRipper.Core.SiteParsing.VideoCapturers;

public class OmegaScansVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.StartsWith("https://api.omegascans.org/chapter/query");
    }

    protected override string GetId(string url)
    {
        return url;
    }
}