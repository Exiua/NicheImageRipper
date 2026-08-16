using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Exceptions;
using NicheImageRipper.Core.FileDownloading;
using NicheImageRipper.Core.SiteParsing.LinkRules;

namespace NicheImageRipper.SiteModules.Modules.Google;

public sealed class GDriveLinkRule : IExpandingSiteLinkRule
{
    public string RuleName => "gdrive";
    public bool Matches(string url) => url.Contains("drive.google.com");
    public SiteLinkInfo Resolve(string url) =>
        throw new RipperException(
            $"GDrive URL reached FileLink construction directly instead of being expanded via RipInfo: {url}");

    public Task<(List<FileLink>, int)> ExpandAsync(string url, int startIndex, FilenameScheme scheme) =>
        GDriveHelper.QueryGDriveLinks(url, startIndex, scheme);
}