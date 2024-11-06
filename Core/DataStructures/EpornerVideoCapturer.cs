namespace Core.DataStructures;

public class EpornerVideoCapturer : PlaylistCapturer
{
    protected override string SearchPattern => "master.m3u8";
    
    protected override string GetId(string url)
    {
        return url.Split("hash=")[1].Split("&")[0];
    }
}