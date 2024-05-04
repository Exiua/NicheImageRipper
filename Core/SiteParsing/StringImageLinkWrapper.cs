using Core.DataStructures;
using Core.Exceptions;

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

    public override string ToString()
    {
        return Url ?? ImageLink?.Url ?? throw new RipperException("StringImageLinkWrapper is empty.");
    }

    public static implicit operator StringImageLinkWrapper(string url) => new(url);
    public static implicit operator StringImageLinkWrapper(ImageLink imageLink) => new(imageLink);
    public static implicit operator string(StringImageLinkWrapper wrapper)
    {
        if (wrapper.Url is not null)
        {
            return wrapper.Url;
        }
        if (wrapper.ImageLink is not null)
        {
            return wrapper.ImageLink.Url;
        }

        throw new RipperException("StringImageLinkWrapper is empty.");
    }
}