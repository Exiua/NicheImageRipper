using OpenQA.Selenium.BiDi.Modules.Network;

namespace Core.DataStructures.VideoCapturers;

public class Cosplay69VideoCapturer : PlaylistCapturer
{
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains("master.m3u8");
    }

    protected override string GetId(string url)
    {
        return url.Split("f=")[1].Split("&")[0];
    }
}