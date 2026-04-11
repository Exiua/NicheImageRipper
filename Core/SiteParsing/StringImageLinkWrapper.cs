using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Core.DataStructures;
using Core.Exceptions;

namespace Core.SiteParsing;

public struct StringImageLinkWrapper
{
    public string? Url { get; set; }
    public ImageLink? ImageLink { get; set; }
    
    [MemberNotNullWhen(true, nameof(ImageLink))]
    [MemberNotNullWhen(false, nameof(Url))]
    [JsonIgnore]
    public bool IsImageLink => ImageLink is not null;

    [JsonConstructor]
    public StringImageLinkWrapper()
    {
        
    }
    
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

    public bool StartsWith(string value)
    {
        return Url?.StartsWith(value) ?? ImageLink?.Url.StartsWith(value) ?? false;
    }
    
    public bool Contains(string value)
    {
        return Url?.Contains(value) ?? ImageLink?.Url.Contains(value) ?? false;
    }
    
    public bool EndsWith(string value)
    {
        return Url?.EndsWith(value) ?? ImageLink?.Url.EndsWith(value) ?? false;
    }
    
    public StringImageLinkWrapper Replace(string oldValue, string newValue)
    {
        return Url?.Replace(oldValue, newValue) ?? ImageLink?.Url.Replace(oldValue, newValue) ?? throw new RipperException("StringImageLinkWrapper is empty.");
    }
    
    public string[] Split(string separator)
    {
        return Url?.Split(separator) ?? ImageLink?.Url.Split(separator) ?? throw new RipperException("StringImageLinkWrapper is empty.");
    }
    
    public override string ToString()
    {
        return Url ?? ImageLink?.Url ?? throw new RipperException("StringImageLinkWrapper is empty.");
    }

    // Debugging Use Only
    private bool IsInvalid()
    {
        return Url is null && ImageLink is null;
    }

    public static implicit operator StringImageLinkWrapper(string url) => new(url);
    public static implicit operator StringImageLinkWrapper(ImageLink imageLink) => new(imageLink);
    public static implicit operator string(StringImageLinkWrapper wrapper)
    {
        if (wrapper.Url is not null)
        {
            return wrapper.Url;
        }
        
        return wrapper.ImageLink is not null 
            ? wrapper.ImageLink.Url 
            : throw new RipperException("StringImageLinkWrapper is empty.");
    }
}