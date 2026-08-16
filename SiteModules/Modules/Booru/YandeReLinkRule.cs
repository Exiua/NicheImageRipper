using NicheImageRipper.Common.ExtensionMethods;
using NicheImageRipper.Core.SiteParsing.LinkRules;

namespace NicheImageRipper.SiteModules.Modules.Booru;

public sealed class YandeReLinkRule : ISiteLinkRule
{
    public string RuleName => "yande.re";
    public bool Matches(string url) => url.Contains("yande.re/");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("/")[^1].Remove("yande.re").Replace("%20", "-");
        return new SiteLinkInfo(url, Filename: filename);
    }
}
