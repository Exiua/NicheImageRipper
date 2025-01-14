using OpenQA.Selenium.BiDi.Modules.Network;

namespace Core.DataStructures.VideoCapturers;

public class JieAvCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("index.m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("/")[^2];
    }
}