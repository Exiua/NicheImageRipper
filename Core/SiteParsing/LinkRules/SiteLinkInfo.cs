using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing.LinkRules;

public readonly record struct SiteLinkInfo(
    string Url,
    LinkInfo? LinkInfo = null,
    string? Referer = null,
    string? Filename = null);