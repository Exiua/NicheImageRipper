using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

public sealed class KemonoCoomerLinkRule : ISiteLinkRule
{
    public string RuleName => "kemono-coomer";
    public bool Matches(string url) =>
        (url.Contains("kemono.su/") || url.Contains("coomer.su/")) && url.Contains("?f=");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("?f=")[^1];
        if (filename.Contains("http"))
        {
            filename = url.Split("?f=")[0].Split("/")[^1];
        }
        return new SiteLinkInfo(url, Filename: filename);
    }
}