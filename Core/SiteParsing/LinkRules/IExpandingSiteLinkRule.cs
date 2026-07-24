using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules;

public interface IExpandingSiteLinkRule : ISiteLinkRule
{
    Task<(List<FileLink> FileLinks, int NextIndex)> ExpandAsync(string url, int startIndex, FilenameScheme scheme);
}