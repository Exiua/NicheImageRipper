using NicheImageRipper.Sdk.DataStructures;

namespace NicheImageRipper.Sdk.SiteParsing;

public readonly record struct SiteLinkInfo(
    string Url,
    LinkInfo? LinkInfo = null,
    string? Referer = null,
    string? Filename = null);