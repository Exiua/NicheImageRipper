using Core.DataStructures;

namespace Core;

public struct StringImageLinkWrapper
{
    public string? Url { get; set; }
    public ImageLink? ImageLink { get; set; }
    
    public StringImageLinkWrapper(string url)
    {
        Url = url;
        ImageLink = null;
    }
    
    public StringImageLinkWrapper(ImageLink imageLink)
    {
        Url = null;
        ImageLink = imageLink;
    }
    
    public static implicit operator StringImageLinkWrapper(string url) => new(url);
    public static implicit operator StringImageLinkWrapper(ImageLink imageLink) => new(imageLink);
}