using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.SiteParsing.LinkRules;

namespace NicheImageRipper.SiteModules.Modules.Ddyunbo;

public sealed class DdyunboLinkRule : ISiteLinkRule
{
    public string RuleName => "ddyunbo";
    public bool Matches(string url) => url.Contains("ddyunbo.com");

    public SiteLinkInfo Resolve(string url) =>
        new(url, LinkInfo.M3U8Ffmpeg, Filename: url.Split("/")[^2] + ".mp4");
}