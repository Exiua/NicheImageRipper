using Sdk.DataStructures;

namespace Sdk.SiteParsing;

public readonly record struct SiteLinkInfo(
    string Url,
    LinkInfo? LinkInfo = null,
    string? Referer = null,
    string? Filename = null);