
using Sdk.DataStructures;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Pornhub;

public sealed class PhncdnLinkRule : ISiteLinkRule
{
    public string RuleName => "phncdn";
    public bool Matches(string url) => url.Contains("phncdn.com");

    public SiteLinkInfo Resolve(string url)
    {
        var parts = url.Split("/");
        var filename = parts.Length >= 9 ? parts[8] : parts[^1].Split(")")[0];
        filename = filename.Split('?')[0];
        var linkInfo = url.Contains(".m3u8") ? LinkInfo.ObfuscatedM3U8 : LinkInfo.None;
        return new SiteLinkInfo(url, linkInfo, Filename: filename);
    }
}