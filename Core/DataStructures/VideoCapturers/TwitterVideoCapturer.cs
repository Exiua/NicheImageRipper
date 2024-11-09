using OpenQA.Selenium.BiDi.Modules.Network;

namespace Core.DataStructures.VideoCapturers;

public class TwitterVideoCapturer : PlaylistCapturer
{
    protected override string GetId(string url)
    {
        return url.Split("/")[4];
    }
    
    protected override bool ResponseIsInteresting(ResponseCompletedEventArgs e)
    {
        return e.Response.Url.Contains(".m3u8");
    }
}