namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class Rule34VideoLinkRule : ISiteLinkRule
{
    public string RuleName => "rule34video";
    public bool Matches(string url) => url.Contains("rule34video.com");

    public SiteLinkInfo Resolve(string url)
    {
        var filename = url.Split("download_filename=")[1].Split("&")[0];
        return new SiteLinkInfo(url, Filename: filename);
    }
}