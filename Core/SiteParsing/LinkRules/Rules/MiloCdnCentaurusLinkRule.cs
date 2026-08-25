using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using Sdk.DataStructures;
using Sdk.SiteParsing;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class MiloCdnCentaurusLinkRule : ISiteLinkRule
{
    public string RuleName => "milocdn-centaurus";
    public bool Matches(string url) =>
        (url.Contains("milocdn.com") || url.Contains("cdn-centaurus.com")) && url.Contains("master.m3u8");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("t=")[1].Split("&")[0] + ".mp4";
        return new SiteLinkInfo(url, LinkInfo.M3U8YtDlp, Filename: filename);
    }
}