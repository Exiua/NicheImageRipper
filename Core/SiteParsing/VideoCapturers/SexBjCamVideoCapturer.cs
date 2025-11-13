using Core.Utility;
using OpenQA.Selenium.BiDi.Network;

namespace Core.SiteParsing.VideoCapturers;

public class SexBjCamVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("master.m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[4];
    }
}