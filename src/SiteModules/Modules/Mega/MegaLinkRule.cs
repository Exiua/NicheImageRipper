using MegaSdk;
using NicheImageRipper.Sdk.DataStructures;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.SiteParsing;
using Serilog;

namespace NicheImageRipper.SiteModules.Modules.Mega;

/// <summary>
///     Mega links resolve to <see cref="MegaLinkInfo.Mega" />, and file and folder links are expanded into one link
///     per file (wherever they're found, including links embedded in other sites' pages).
/// </summary>
public sealed class MegaLinkRule : IExpandingSiteLinkRule
{
    private static readonly ILogger Logger = Log.ForContext<MegaLinkRule>();

    public string RuleName => "mega";
    public bool Matches(string url) => MegaLink.TryParse(url, out _);
    public SiteLinkInfo Resolve(string url) => new(url, MegaLinkInfo.Mega);

    public async Task<(List<FileLink> FileLinks, int NextIndex)> ExpandAsync(string url, int startIndex,
                                                                           FilenameScheme scheme)
    {
        try
        {
            return await MegaHelper.ExpandAsync(url, startIndex, scheme);
        }
        catch (Exception e) when (e is MegaException or FeatureNotAvailableException)
        {
            // Don't fail the whole rip over one Mega link (e.g. a dead link embedded in another site's page). Keep it
            // unexpanded so the download reports it as failed.
            Logger.Warning(e, "Unable to expand Mega link {Url}", url);
            return ([FileLink.Create(url, scheme, startIndex, MegaLinkInfo.Mega)], startIndex + 1);
        }
    }
}
