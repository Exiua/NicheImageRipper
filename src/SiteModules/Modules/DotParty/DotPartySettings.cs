using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Enums;

namespace NicheImageRipper.SiteModules.Modules.DotParty;

/// <summary>
///     Kemono, coomer and pawchive settings, read from each site config's Custom JSON, e.g.
///     <c>{ "CreatorFilenameSchemes": [{ "Service": "patreon", "UserId": "12345", "FilenameScheme": "Hash" }] }</c>.
/// </summary>
public class DotPartySettings
{
    [SiteSetting("Creator filename schemes", ItemLabel = "Creator",
        Description = "Filename schemes for specific creators, used instead of the site's. A creator is identified " +
                      "by the service and user ID in their URL, e.g. patreon and 12345 for /patreon/user/12345.")]
    public List<CreatorFilenameScheme> CreatorFilenameSchemes { get; set; } = [];

    /// <summary>The scheme set for the creator, or null when there's none (the last entry wins on duplicates).</summary>
    public FilenameScheme? GetFilenameScheme(string service, string userId) =>
        CreatorFilenameSchemes.LastOrDefault(c => c.Matches(service, userId) && Enum.IsDefined(c.FilenameScheme))
                              ?.FilenameScheme;
}

public class CreatorFilenameScheme
{
    [SiteSetting("Service", Description = "e.g. patreon, fanbox, fantia, onlyfans")]
    public string Service { get; set; } = "";

    [SiteSetting("User ID")]
    public string UserId { get; set; } = "";

    [SiteSetting("Filename scheme")]
    public FilenameScheme FilenameScheme { get; set; } = FilenameScheme.Original;

    public bool Matches(string service, string userId) =>
        string.Equals(Service.Trim(), service, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(UserId.Trim(), userId, StringComparison.OrdinalIgnoreCase);
}
