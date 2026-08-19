using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Utility;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class Base64LinkRule : ISiteLinkRule
{
    public string RuleName => "base64";

    public bool Matches(string url) => url.StartsWith("data:image/") && url.Contains(";base64,");

    public SiteLinkInfo Resolve(string url)
    {
        var ext = url.Split("image/")[1].Split(";")[0];
        var filename = $"{StringUtility.HashStringMd5(url)}.{ext}";
        return new SiteLinkInfo(url, LinkInfo.Base64, Filename: filename);
    }
}