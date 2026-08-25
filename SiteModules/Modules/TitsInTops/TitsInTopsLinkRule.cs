using System.Text.RegularExpressions;
using Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.TitsInTops;

public sealed partial class TitsInTopsLinkRule : ISiteLinkRule
{
    public string RuleName => "titsintops";
    public bool Matches(string url) => url.Contains("https://titsintops.com/") && url[^1] == '/';

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("/")[^2];
        filename = ExtensionRegex().Replace(filename, ".$1");
        return new SiteLinkInfo(url, Filename: filename);
    }

    [GeneratedRegex(@"-(jpg|png|webp|mp4|mov|avi|wmv)\.\d+/?")]
    private static partial Regex ExtensionRegex();
}