using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.SiteParsing;

namespace NicheImageRipper.SiteModules.Modules.Cdn;

public sealed class OkCdnVkvdLinkRule : ISiteLinkRule
{
    public string RuleName => "okcdn-vkvd";
    public bool Matches(string url) => url.Contains("//vkvd") && url.Contains("okcdn.ru");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Contains("id=")
            ? url.Split("id=")[1].Split("&")[0] + ".mp4"
            : url.Split(".")[^2] + ".mp4";
        return new SiteLinkInfo(url, LinkInfo.MpegDash, Filename: filename);
    }
}