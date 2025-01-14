using OpenQA.Selenium.BiDi.Modules.Network;
using Serilog;

namespace Core.DataStructures.VideoCapturers;

public class SexBjCamVideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("master.m3u8");
    }

    protected override string GetId(string url)
    {
        return GetUrlParameterValue(url, "t");
    }
}