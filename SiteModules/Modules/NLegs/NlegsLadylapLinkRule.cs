
using Sdk.DataStructures;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.NLegs;

public sealed class NlegsLadylapLinkRule : ISiteLinkRule
{
    public string RuleName => "nlegs-ladylap";
    public bool Matches(string url) => url.Contains("nlegs.com") || url.Contains("ladylap.com");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("url=")[1].Split("&")[0] + ".jfif";
        return new SiteLinkInfo(url, LinkInfo.ResolveImage, Filename: filename);
    }
}