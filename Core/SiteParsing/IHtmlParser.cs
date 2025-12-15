using Core.Configuration;
using Core.Enums;

namespace Core.SiteParsing;

public interface IHtmlParser
{
    static abstract string ParserName { get; }

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