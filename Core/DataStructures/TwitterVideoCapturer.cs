namespace Core.DataStructures;

public class TwitterVideoCapturer : PlaylistCapturer
{
    protected override string SearchPattern => ".m3u8";

    protected override string GetId(string url)
    {
        return url.Split("/")[4];
    }
}