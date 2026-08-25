using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Sdk.DataStructures;
using Sdk.Exceptions;

namespace Sdk.SiteParsing;

public struct StringFileLinkWrapper
{
    public string? Url { get; set; }
    public FileLink? FileLink { get; set; }

    [MemberNotNullWhen(true, nameof(FileLink))]
    [MemberNotNullWhen(false, nameof(Url))]
    [JsonIgnore]
    public bool IsFileLink => FileLink is not null;

    [JsonConstructor]
    public StringFileLinkWrapper()
    {
    }

    public StringFileLinkWrapper(string url)
    {
        Url = url;
        FileLink = null;
    }

    public StringFileLinkWrapper(FileLink fileLink)
    {
        Url = null;
        FileLink = fileLink;
    }

    public bool StartsWith(string value)
    {
        return Url?.StartsWith(value) ?? FileLink?.Url.StartsWith(value) ?? false;
    }

    public bool Contains(string value)
    {
        return Url?.Contains(value) ?? FileLink?.Url.Contains(value) ?? false;
    }

    public bool EndsWith(string value)
    {
        return Url?.EndsWith(value) ?? FileLink?.Url.EndsWith(value) ?? false;
    }

    public StringFileLinkWrapper Replace(string oldValue, string newValue)
    {
        return Url?.Replace(oldValue, newValue) ?? FileLink?.Url.Replace(oldValue, newValue) ??
            throw new RipperException("StringImageLinkWrapper is empty.");
    }

    public string[] Split(string separator)
    {
        return Url?.Split(separator) ?? FileLink?.Url.Split(separator) ??
            throw new RipperException("StringImageLinkWrapper is empty.");
    }

    public override string ToString()
    {
        return Url ?? FileLink?.Url ?? throw new RipperException("StringImageLinkWrapper is empty.");
    }

    // Debugging Use Only
    private bool IsInvalid()
    {
        return Url is null && FileLink is null;
    }

    public static implicit operator StringFileLinkWrapper(string url) => new(url);
    public static implicit operator StringFileLinkWrapper(FileLink fileLink) => new(fileLink);

    public static implicit operator string(StringFileLinkWrapper wrapper)
    {
        if (wrapper.Url is not null)
        {
            return wrapper.Url;
        }

        return wrapper.FileLink is not null
            ? wrapper.FileLink.Url
            : throw new RipperException("StringImageLinkWrapper is empty.");
    }
}