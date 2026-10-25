using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Cdn;

public sealed class SendvidLinkRule : ISiteLinkRule
{
    public string RuleName => "sendvid";
    public bool Matches(string url) => url.Contains("sendvid.com") && url.Contains(".m3u8");

    public SiteLinkInfo Resolve(string url) =>
        new(url, LinkInfo.M3U8Ffmpeg, Filename: url.Split("/")[6]);
}