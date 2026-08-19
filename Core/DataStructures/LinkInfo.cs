using System.Data;
using Dapper;

namespace NicheImageRipper.Core.DataStructures;

/// <summary>
///     Identifies which download mechanism a FileLink requires. Core defines the generic, non-site-specific
///     mechanisms as constants below; site modules define their own string constants for site-specific
///     mechanisms (e.g. "mega", "gdrive") next to whichever IFileDownloadStrategy handles them.
/// </summary>
public readonly record struct LinkInfo
{
    internal LinkInfo(string Name)
    {
        this.Name = Name;
    }

    private const string NoneValue = "none";
    private const string TextValue = "text";
    private const string Base64Value = "base64";
    private const string ResolveImageValue = "resolve-image";
    private const string SeleniumImageValue = "selenium-image";
    private const string M3U8FfmpegValue = "m3u8-ffmpeg";
    private const string M3U8YtDlpValue = "m3u8-ytdlp";
    private const string ObfuscatedM3U8Value = "obfuscated-m3u8";
    private const string MpegDashValue = "mpeg-dash";
    private const string IframeMediaValue = "iframe-media";

    public static readonly LinkInfo None = LinkInfoRegistry.Register(NoneValue);
    public static readonly LinkInfo Text = LinkInfoRegistry.Register(TextValue);
    public static readonly LinkInfo Base64 = LinkInfoRegistry.Register(Base64Value);
    public static readonly LinkInfo ResolveImage = LinkInfoRegistry.Register(ResolveImageValue);
    public static readonly LinkInfo SeleniumImage = LinkInfoRegistry.Register(SeleniumImageValue);
    public static readonly LinkInfo M3U8Ffmpeg = LinkInfoRegistry.Register(M3U8FfmpegValue);
    public static readonly LinkInfo M3U8YtDlp = LinkInfoRegistry.Register(M3U8YtDlpValue);
    public static readonly LinkInfo ObfuscatedM3U8 = LinkInfoRegistry.Register(ObfuscatedM3U8Value);
    public static readonly LinkInfo MpegDash = LinkInfoRegistry.Register(MpegDashValue);
    public static readonly LinkInfo IframeMedia = LinkInfoRegistry.Register(IframeMediaValue);

    public string Name { get; init; }

    public bool Equals(LinkInfo other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    public override string ToString() => Name;

    public void Deconstruct(out string name)
    {
        name = Name;
    }
    
    public static implicit operator string(LinkInfo linkInfo) => linkInfo.Name;
    public static implicit operator LinkInfo(string linkInfoName) => LinkInfoRegistry.Get(linkInfoName);
}

public static class LinkInfoRegistry
{
    private static readonly Dictionary<string, LinkInfo> LinkInfos = new();

    static LinkInfoRegistry()
    {
        SqlMapper.AddTypeHandler(new LinkInfoTypeHandler());
    }

    public static LinkInfo Register(string name)
    {
        LinkInfos[name] = new LinkInfo(name);
        return LinkInfos[name];
    }

    public static LinkInfo Get(string name)
    {
        if (LinkInfos.TryGetValue(name, out var linkInfo))
        {
            return linkInfo;
        }

        throw new KeyNotFoundException($"LinkInfo with name '{name}' is not registered.");
    }
}

public sealed class LinkInfoTypeHandler : SqlMapper.TypeHandler<LinkInfo>
{
    public override LinkInfo Parse(object value)
    {
        return LinkInfoRegistry.Get((string)value);
    }

    public override void SetValue(IDbDataParameter parameter, LinkInfo value)
    {
        parameter.Value = value.Name;
        parameter.DbType = DbType.String;
    }
}