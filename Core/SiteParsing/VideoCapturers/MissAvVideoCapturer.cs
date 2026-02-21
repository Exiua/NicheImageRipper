using OpenQA.Selenium.BiDi.Network;

namespace Core.SiteParsing.VideoCapturers;

public class MissAvVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("playlist.m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[3];
    }
}