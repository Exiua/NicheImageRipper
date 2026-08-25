using Sdk.DataStructures;
using Sdk.Enums;

namespace Sdk.SiteParsing;

public interface IExpandingSiteLinkRule : ISiteLinkRule
{
    Task<(List<FileLink> FileLinks, int NextIndex)> ExpandAsync(string url, int startIndex, FilenameScheme scheme);
}