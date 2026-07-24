namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class PlayhlsLinkRule : ISiteLinkRule
{
    public string RuleName => "playhls";
    public bool Matches(string url) => url.Contains("playhls.com/");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("&id=")[^1].Split("&")[0] + ".mp4";
        return new SiteLinkInfo(url, Filename: filename);
    }
}