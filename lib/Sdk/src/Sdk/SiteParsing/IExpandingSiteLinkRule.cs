using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.Sdk.SiteParsing;

public interface IExpandingSiteLinkRule : ISiteLinkRule
{
    Task<(List<FileLink> FileLinks, int NextIndex)> ExpandAsync(string url, int startIndex, FilenameScheme scheme);
}