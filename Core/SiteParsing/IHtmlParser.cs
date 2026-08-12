using NicheImageRipper.Core.Configuration;
using NicheImageRipper.Core.Enums;

namespace NicheImageRipper.Core.SiteParsing;

public interface IHtmlParser
{
    static abstract string ParserName { get; }

    /// <summary>The full base URLs (scheme + host + trailing slash) this parser supports, matching how
    /// UrlUtility.UrlCheck normalizes a URL for comparison (e.g. "https://www.example.com/").</summary>
    static abstract string[] SupportedUrls { get; }

    static FilenameScheme GetFilenameScheme<T>(FilenameScheme original) where T : IHtmlParser
    {
        var config = Config.Instance;
        if (!config.ParserSpecificSettingsOverrides.TryGetValue(T.ParserName, out var settingsOverride))
        {
            return original;
        }

        var overrideFilenameScheme = settingsOverride.FilenameScheme;
        return Enum.IsDefined(overrideFilenameScheme) ? overrideFilenameScheme : original;
    }
}

/// <summary>
/// Implemented by parsers that should also be registered under additional site names (e.g. mirror domains).
/// </summary>
public interface IMultiSiteHtmlParser
{
    static abstract string[] AdditionalParserNames { get; }
}