using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class XxMediaKnitBidLinkRule : ISiteLinkRule
{
    public string RuleName => "xx-media-knit-bid";
    public bool Matches(string url) => url.Contains("xx-media.knit.bid");

    public SiteLinkInfo Resolve(string url) =>
        new(url, LinkInfo.SeleniumImage, Filename: url.Split("/")[^1]);
}