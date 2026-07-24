using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules.Rules;

public sealed class GDriveLinkRule : ISiteLinkRule
{
    public string RuleName => "gdrive";
    public bool Matches(string url) => url.Contains("drive.google.com");
    public SiteLinkInfo Resolve(string url) => new(url, LinkInfo.GDrive);
    // Filename intentionally omitted — GDrive filenames come from the Drive API, supplied by the caller
}